using System.Diagnostics;
using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Models.Preprocessing;
using DocProcessing.Application.Services.Preprocessing;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Application.Pipeline;

/// <summary>
/// Preprocessing stage activity that normalizes OCR output.
/// </summary>
public sealed partial class PreprocessStageActivity : IJobStageActivity
{
    private readonly ILogger<PreprocessStageActivity> _logger;
    private readonly IStorageService _storageService;
    private readonly PreprocessOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ITextNormalizer _textNormalizer;
    private readonly ITableConverter _tableConverter;
    private readonly IFieldParser _fieldParser;

    public string StageName => "Preprocess";
    public ProcessJobStage Stage => ProcessJobStage.Preprocess;

    public PreprocessStageActivity(
        ILogger<PreprocessStageActivity> logger,
        IStorageService storageService,
        IOptions<PreprocessOptions> options,
        TimeProvider timeProvider,
        ITextNormalizer textNormalizer,
        ITableConverter tableConverter,
        IFieldParser fieldParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _textNormalizer = textNormalizer ?? throw new ArgumentNullException(nameof(textNormalizer));
        _tableConverter = tableConverter ?? throw new ArgumentNullException(nameof(tableConverter));
        _fieldParser = fieldParser ?? throw new ArgumentNullException(nameof(fieldParser));
    }

    public async Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        LogPreprocessStageStarting(_logger, context.Job.JobId, context.CorrelationId);

        try
        {
            // Extract OCR blob path from metadata
            if (!context.Metadata.TryGetValue(StageMetadataKeys.OcrBlobPath, out var ocrBlobPathObj) ||
                ocrBlobPathObj is not string ocrBlobPath)
            {
                LogOcrBlobPathNotFound(_logger, context.Job.JobId, context.CorrelationId);

                return StageResult.Failure(
                    errorCode: "PREPROCESS_MISSING_OCR_PATH",
                    errorMessage: "OCR blob path not found in stage context metadata");
            }

            // Download OCR result from blob storage
            var ocrResult = await _storageService.DownloadJsonAsync<OcrResult>(
                "ocr-results",
                ocrBlobPath,
                cancellationToken);

            if (ocrResult == null)
            {
                LogOcrResultNotFound(_logger, ocrBlobPath, context.Job.JobId, context.CorrelationId);

                return StageResult.Failure(
                    errorCode: "PREPROCESS_OCR_NOT_FOUND",
                    errorMessage: $"OCR result not found at blob path: {ocrBlobPath}");
            }

            // Process pages
            var pages = ProcessPages(ocrResult);

            // Process tables
            var tables = ProcessTables(ocrResult);

            // Process form fields
            var formFields = ProcessFormFields(ocrResult);

            // Calculate totals
            var totalWordCount = pages.Sum(p => p.WordCount);

            // Get tenant ID from metadata
            var tenantId = context.Metadata.TryGetValue(StageMetadataKeys.TenantId, out var tenantIdObj)
                ? tenantIdObj.ToString()
                : "default";

            // Create preprocess result
            var preprocessResult = new PreprocessResult
            {
                DocumentId = context.Job.DocumentId,
                JobId = context.Job.JobId,
                Pages = pages,
                Tables = tables,
                FormFields = formFields,
                Metadata = new PreprocessMetadata
                {
                    ProcessedAt = _timeProvider.GetUtcNow(),
                    ProcessingDuration = stopwatch.Elapsed,
                    PageCount = pages.Count,
                    TotalWordCount = totalWordCount,
                    TotalTables = tables.Count,
                    TotalFormFields = formFields.Count,
                    PrimaryLanguage = ocrResult.Metadata.PrimaryLanguage,
                    NormalizationSettings = new Dictionary<string, bool>
                    {
                        ["UnicodeNormalization"] = _options.EnableUnicodeNormalization,
                        ["WhitespaceCleanup"] = _options.EnableWhitespaceCleanup,
                        ["TableConversion"] = _options.ConvertTablesToStructured
                    },
                    Warnings = []
                }
            };

            // Upload preprocessed result to blob storage
            var blobPath = $"{_options.OutputBlobContainer}/{tenantId}/{context.Job.DocumentId}/preprocess-result.json";
            var uploadedBlobPath = await _storageService.UploadJsonAsync(
                _options.OutputBlobContainer,
                blobPath,
                preprocessResult,
                cancellationToken);

            stopwatch.Stop();

            LogPreprocessStageCompleted(
                _logger,
                context.Job.JobId,
                pages.Count,
                tables.Count,
                formFields.Count,
                stopwatch.ElapsedMilliseconds);

            return StageResult.Success(
                output: null,
                metadata: new Dictionary<string, object>
                {
                    [StageMetadataKeys.PreprocessBlobPath] = uploadedBlobPath,
                    ["pageCount"] = pages.Count,
                    ["totalWordCount"] = totalWordCount,
                    ["totalTables"] = tables.Count,
                    ["totalFormFields"] = formFields.Count,
                    ["processingDurationMs"] = stopwatch.ElapsedMilliseconds
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            LogPreprocessStageFailed(_logger, ex, context.Job.JobId, context.CorrelationId);

            return StageResult.Failure(
                errorCode: "PREPROCESS_ERROR",
                errorMessage: ex.Message);
        }
    }

    private IReadOnlyList<PreprocessedPage> ProcessPages(OcrResult ocrResult)
    {
        var pages = new List<PreprocessedPage>();

        foreach (var ocrPage in ocrResult.Pages)
        {
            var normalizedTextBlocks = new List<NormalizedTextBlock>();
            var pageTextParts = new List<string>();

            foreach (var textBlock in ocrPage.TextBlocks)
            {
                var normalizedText = _textNormalizer.NormalizeText(textBlock.Text);

                normalizedTextBlocks.Add(new NormalizedTextBlock
                {
                    OriginalText = textBlock.Text,
                    NormalizedText = normalizedText,
                    BlockType = textBlock.BlockType,
                    Confidence = textBlock.Confidence,
                    BoundingBox = textBlock.BoundingBox,
                    PageNumber = ocrPage.PageNumber
                });

                pageTextParts.Add(normalizedText);
            }

            var fullPageText = string.Join("\n", pageTextParts);
            var wordCount = fullPageText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

            pages.Add(new PreprocessedPage
            {
                PageNumber = ocrPage.PageNumber,
                NormalizedText = fullPageText,
                TextBlocks = normalizedTextBlocks,
                Language = ocrPage.Language,
                WordCount = wordCount
            });
        }

        return pages;
    }

    private IReadOnlyList<StructuredTable> ProcessTables(OcrResult ocrResult)
    {
        var tables = new List<StructuredTable>();
        var tableNumber = 1;

        foreach (var ocrPage in ocrResult.Pages)
        {
            foreach (var ocrTable in ocrPage.Tables)
            {
                var structuredTable = _tableConverter.ConvertToStructured(ocrTable);

                // Update table number (create new instance with updated table number)
                tables.Add(new StructuredTable
                {
                    TableNumber = tableNumber++,
                    PageNumber = structuredTable.PageNumber,
                    Headers = structuredTable.Headers,
                    Rows = structuredTable.Rows,
                    JsonRepresentation = structuredTable.JsonRepresentation,
                    CsvRepresentation = structuredTable.CsvRepresentation,
                    Confidence = structuredTable.Confidence,
                    BoundingBox = structuredTable.BoundingBox
                });
            }
        }

        return tables;
    }

    private IReadOnlyList<NormalizedFormField> ProcessFormFields(OcrResult ocrResult)
    {
        var formFields = new List<NormalizedFormField>();

        foreach (var ocrPage in ocrResult.Pages)
        {
            foreach (var formField in ocrPage.FormFields)
            {
                var normalizedField = _fieldParser.ParseField(formField);
                formFields.Add(normalizedField);
            }
        }

        return formFields;
    }

    #region Logging

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting Preprocess stage for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogPreprocessStageStarting(
        ILogger logger,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "OCR blob path not found in metadata. JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogOcrBlobPathNotFound(
        ILogger logger,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "OCR result not found at path: {OcrBlobPath}. JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogOcrResultNotFound(
        ILogger logger,
        string ocrBlobPath,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Preprocess stage completed successfully for JobId: {JobId}. Pages: {PageCount}, Tables: {TableCount}, Fields: {FieldCount}, Duration: {DurationMs}ms")]
    private static partial void LogPreprocessStageCompleted(
        ILogger logger,
        Guid jobId,
        int pageCount,
        int tableCount,
        int fieldCount,
        long durationMs);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Preprocess stage failed for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogPreprocessStageFailed(
        ILogger logger,
        Exception exception,
        Guid jobId,
        string correlationId);

    #endregion
}
