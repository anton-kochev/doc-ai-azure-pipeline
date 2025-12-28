using System.Diagnostics;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using DocProcessing.Application.Configuration;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Services.OCR;
using DocProcessing.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Infrastructure.Services.OCR;

/// <summary>
/// Azure Document Intelligence implementation of OCR service.
/// Uses Azure.AI.DocumentIntelligence SDK to analyze documents and extract text, tables, and form fields.
/// </summary>
public sealed partial class AzureDocumentIntelligenceOcrService : IOcrService
{
    /// <summary>
    /// Default page width for documents without explicit dimensions.
    /// Represents US Letter width (8.5 inches at 72 DPI = 612 points).
    /// </summary>
    private const double DefaultPageWidthPoints = 612.0;

    /// <summary>
    /// Default page height for documents without explicit dimensions.
    /// Represents US Letter height (11 inches at 72 DPI = 792 points).
    /// </summary>
    private const double DefaultPageHeightPoints = 792.0;

    private readonly OcrOptions _options;
    private readonly ILogger<AzureDocumentIntelligenceOcrService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DocumentIntelligenceClient _client;

    public AzureDocumentIntelligenceOcrService(
        IOptions<OcrOptions> options,
        ILogger<AzureDocumentIntelligenceOcrService> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;

        // Validate configuration
        if (string.IsNullOrWhiteSpace(_options.DocumentIntelligenceEndpoint))
        {
            throw new InvalidOperationException(
                "DocumentIntelligence endpoint must be configured. " +
                "Set Ocr:DocumentIntelligenceEndpoint in configuration.");
        }

        // Initialize Document Intelligence client with Managed Identity
        var endpoint = new Uri(_options.DocumentIntelligenceEndpoint);
        var credential = new DefaultAzureCredential();
        _client = new DocumentIntelligenceClient(endpoint, credential);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para><strong>Stream Ownership:</strong></para>
    /// <para>
    /// The <paramref name="documentStream"/> is read but NOT disposed by this method.
    /// The caller retains ownership and is responsible for disposing the stream.
    /// </para>
    /// <para><strong>Stream Position:</strong></para>
    /// <para>
    /// If the stream supports seeking (<see cref="Stream.CanSeek"/>), the position is
    /// reset to 0 after reading. Non-seekable streams (e.g., network streams) remain
    /// at the end position after this call.
    /// </para>
    /// <para><strong>Stream Requirements:</strong></para>
    /// <list type="bullet">
    /// <item>Must be readable (<see cref="Stream.CanRead"/> returns true)</item>
    /// <item>Must contain valid document data (PDF, PNG, JPEG, etc.)</item>
    /// <item>Recommended: Use seekable streams (e.g., FileStream, MemoryStream) for reusability</item>
    /// </list>
    /// </remarks>
    /// <param name="documentId">
    /// The unique identifier of the document being analyzed. Used for correlation and logging.
    /// </param>
    /// <param name="jobId">
    /// The unique identifier of the processing job. Used for correlation and logging.
    /// </param>
    /// <param name="documentStream">
    /// The document stream to analyze. Must be readable and contain valid document data (PDF, PNG, JPEG, etc.).
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the OCR operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="documentStream"/> is not readable, or when <paramref name="documentId"/>
    /// or <paramref name="jobId"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public async Task<OcrResult> AnalyzeDocumentAsync(
        Guid documentId,
        Guid jobId,
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        if (!documentStream.CanRead)
        {
            throw new ArgumentException("Stream must be readable", nameof(documentStream));
        }

        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Cannot be empty", nameof(documentId));
        }

        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("Cannot be empty", nameof(jobId));
        }

        var stopwatch = Stopwatch.StartNew();
        DateTimeOffset startTime = _timeProvider.GetUtcNow();

        LogOcrAnalysisStarted(documentId, jobId, _options.ModelId);

        try
        {
            // Prepare document content for analysis
            BinaryData documentContent = BinaryData.FromStream(documentStream);

            // Reset stream position if seekable to allow caller reuse
            if (documentStream.CanSeek)
            {
                documentStream.Position = 0;
            }

            // Call Azure Document Intelligence API
            Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                _options.ModelId,
                documentContent,
                cancellationToken: cancellationToken);

            AnalyzeResult azureResult = operation.Value;

            // Map Azure response to our domain model
            List<OcrPage> pages = MapPages(azureResult);

            // Calculate metadata
            int totalTextBlocks = pages.Sum(p => p.TextBlocks.Count);
            int totalTables = pages.Sum(p => p.Tables.Count);
            int totalFormFields = pages.Sum(p => p.FormFields.Count);
            double overallConfidence = pages.Any()
                ? pages.Average(p => p.Confidence)
                : 0.0;

            stopwatch.Stop();

            var metadata = new OcrMetadata(
                provider: "AzureDocumentIntelligence",
                pageCount: pages.Count,
                processedAt: startTime,
                processingDuration: stopwatch.Elapsed,
                overallConfidence: overallConfidence,
                totalTextBlocks: totalTextBlocks,
                totalTables: totalTables,
                totalFormFields: totalFormFields,
                primaryLanguage: DetectPrimaryLanguage(azureResult),
                modelVersion: azureResult.ModelId,
                status: "Success",
                warnings: Array.Empty<string>());

            var result = new OcrResult(
                documentId: documentId,
                jobId: jobId,
                metadata: metadata,
                pages: pages,
                blobPath: null);

            LogOcrAnalysisCompleted(
                documentId,
                jobId,
                pages.Count,
                stopwatch.ElapsedMilliseconds,
                overallConfidence);

            return result;
        }
        catch (RequestFailedException ex) when (ex.Status == 408)
        {
            LogOcrAnalysisFailed(ex, documentId, jobId);
            throw new OcrProcessingException(
                documentId, jobId, "OCR request timed out", ex);
        }
        catch (RequestFailedException ex)
        {
            LogOcrAnalysisFailed(ex, documentId, jobId);
            throw new OcrProcessingException(
                documentId,
                jobId,
                $"Azure Document Intelligence API error: {ex.Message}",
                ex.Status,
                ex);
        }
        catch (OperationCanceledException)
        {
            LogOcrAnalysisCancelled(documentId, jobId);
            throw;
        }
        catch (Exception ex)
        {
            LogOcrAnalysisFailed(ex, documentId, jobId);
            throw new OcrProcessingException(
                documentId, jobId, "Unexpected error during OCR analysis", ex);
        }
    }

    #region Mapping Methods

    private static List<OcrPage> MapPages(AnalyzeResult azureResult)
    {
        if (azureResult.Pages == null || azureResult.Pages.Count == 0)
        {
            return new List<OcrPage>();
        }

        var pages = new List<OcrPage>();

        for (int i = 0; i < azureResult.Pages.Count; i++)
        {
            DocumentPage azurePage = azureResult.Pages[i];
            int pageNumber = azurePage.PageNumber;

            // Map text blocks from lines
            List<TextBlock> textBlocks = MapTextBlocks(azurePage);

            // Map tables for this page
            List<TableData> tables = MapTablesForPage(azureResult, pageNumber);

            // Map form fields for this page
            List<FormField> formFields = MapFormFieldsForPage(azureResult, pageNumber);

            // Calculate page-level confidence
            double pageConfidence = CalculatePageConfidence(azurePage);

            var page = new OcrPage(
                pageNumber: pageNumber,
                width: azurePage.Width ?? DefaultPageWidthPoints,
                height: azurePage.Height ?? DefaultPageHeightPoints,
                confidence: pageConfidence,
                textBlocks: textBlocks,
                tables: tables,
                formFields: formFields,
                language: null, // Language detection handled at document level
                angle: azurePage.Angle ?? 0.0);

            pages.Add(page);
        }

        return pages;
    }

    private static List<TextBlock> MapTextBlocks(DocumentPage azurePage)
    {
        if (azurePage.Lines == null || azurePage.Lines.Count == 0)
        {
            return new List<TextBlock>();
        }

        var textBlocks = new List<TextBlock>();
        int pageNumber = azurePage.PageNumber;
        double pageWidth = azurePage.Width ?? DefaultPageWidthPoints;
        double pageHeight = azurePage.Height ?? DefaultPageHeightPoints;

        foreach (DocumentLine line in azurePage.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Content))
            {
                continue;
            }

            BoundingBox? boundingBox = null;
            if (line.Polygon != null && line.Polygon.Count >= 4)
            {
                boundingBox = MapBoundingBox(line.Polygon, pageWidth, pageHeight);
            }

            // Azure Document Intelligence doesn't provide per-line confidence in the same way
            // Use word-level confidence if available
            double confidence = CalculateLineConfidence(azurePage, line);

            var textBlock = new TextBlock(
                text: line.Content,
                confidence: confidence,
                pageNumber: pageNumber,
                boundingBox: boundingBox,
                blockType: "line",
                languageCode: null);

            textBlocks.Add(textBlock);
        }

        return textBlocks;
    }

    private static List<TableData> MapTablesForPage(AnalyzeResult azureResult, int pageNumber)
    {
        if (azureResult.Tables == null || azureResult.Tables.Count == 0)
        {
            return new List<TableData>();
        }

        var tables = new List<TableData>();

        foreach (DocumentTable azureTable in azureResult.Tables)
        {
            // Check if this table belongs to the current page
            if (!IsTableOnPage(azureTable, pageNumber))
            {
                continue;
            }

            // Get page dimensions for bounding box normalization
            DocumentPage? page = azureResult.Pages?.FirstOrDefault(p => p.PageNumber == pageNumber);
            double pageWidth = page?.Width ?? DefaultPageWidthPoints;
            double pageHeight = page?.Height ?? DefaultPageHeightPoints;

            List<TableCell> cells = MapTableCells(azureTable, pageWidth, pageHeight);

            BoundingBox? tableBoundingBox = null;
            if (azureTable.BoundingRegions != null && azureTable.BoundingRegions.Count > 0)
            {
                BoundingRegion region = azureTable.BoundingRegions[0];
                if (region.Polygon != null && region.Polygon.Count >= 4)
                {
                    tableBoundingBox = MapBoundingBox(region.Polygon, pageWidth, pageHeight);
                }
            }

            var tableData = new TableData(
                rowCount: azureTable.RowCount,
                columnCount: azureTable.ColumnCount,
                cells: cells,
                pageNumber: pageNumber,
                confidence: 0.95, // Default confidence for tables
                boundingBox: tableBoundingBox);

            tables.Add(tableData);
        }

        return tables;
    }

    private static List<TableCell> MapTableCells(DocumentTable azureTable, double pageWidth, double pageHeight)
    {
        if (azureTable.Cells == null || azureTable.Cells.Count == 0)
        {
            return new List<TableCell>();
        }

        var cells = new List<TableCell>();

        foreach (DocumentTableCell azureCell in azureTable.Cells)
        {
            BoundingBox? cellBoundingBox = null;
            if (azureCell.BoundingRegions != null && azureCell.BoundingRegions.Count > 0)
            {
                BoundingRegion region = azureCell.BoundingRegions[0];
                if (region.Polygon != null && region.Polygon.Count >= 4)
                {
                    cellBoundingBox = MapBoundingBox(region.Polygon, pageWidth, pageHeight);
                }
            }

            var cell = new TableCell(
                rowIndex: azureCell.RowIndex,
                columnIndex: azureCell.ColumnIndex,
                content: azureCell.Content ?? string.Empty,
                confidence: 0.95, // Default confidence
                isHeader: azureCell.Kind == DocumentTableCellKind.ColumnHeader ||
                         azureCell.Kind == DocumentTableCellKind.RowHeader,
                rowSpan: azureCell.RowSpan ?? 1,
                columnSpan: azureCell.ColumnSpan ?? 1,
                boundingBox: cellBoundingBox);

            cells.Add(cell);
        }

        return cells;
    }

    private static List<FormField> MapFormFieldsForPage(AnalyzeResult azureResult, int pageNumber)
    {
        if (azureResult.KeyValuePairs == null || azureResult.KeyValuePairs.Count == 0)
        {
            return [];
        }

        List<FormField> formFields = [];
        DocumentPage? page = azureResult.Pages?.FirstOrDefault(p => p.PageNumber == pageNumber);
        double pageWidth = page?.Width ?? DefaultPageWidthPoints;
        double pageHeight = page?.Height ?? DefaultPageHeightPoints;

        foreach (DocumentKeyValuePair kvp in azureResult.KeyValuePairs)
        {
            // Check if this key-value pair belongs to the current page
            if (!IsKeyValuePairOnPage(kvp, pageNumber))
            {
                continue;
            }

            string key = kvp.Key?.Content ?? string.Empty;
            string value = kvp.Value?.Content ?? string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            BoundingBox? keyBoundingBox = null;
            if (kvp.Key?.BoundingRegions != null && kvp.Key.BoundingRegions.Count > 0)
            {
                BoundingRegion region = kvp.Key.BoundingRegions[0];
                if (region.Polygon != null && region.Polygon.Count >= 4)
                {
                    keyBoundingBox = MapBoundingBox(region.Polygon, pageWidth, pageHeight);
                }
            }

            BoundingBox? valueBoundingBox = null;
            if (kvp.Value?.BoundingRegions != null && kvp.Value.BoundingRegions.Count > 0)
            {
                BoundingRegion region = kvp.Value.BoundingRegions[0];
                if (region.Polygon != null && region.Polygon.Count >= 4)
                {
                    valueBoundingBox = MapBoundingBox(region.Polygon, pageWidth, pageHeight);
                }
            }

            // Confidence is a non-nullable float in Azure SDK
            double confidence = kvp.Confidence;

            var formField = new FormField(
                key: key,
                value: value,
                keyConfidence: confidence,
                valueConfidence: confidence,
                pageNumber: pageNumber,
                keyBoundingBox: keyBoundingBox,
                valueBoundingBox: valueBoundingBox,
                fieldType: null);

            formFields.Add(formField);
        }

        return formFields;
    }

    private static BoundingBox MapBoundingBox(IReadOnlyList<float> polygon, double pageWidth, double pageHeight)
    {
        if (polygon.Count < 8)
        {
            // Not enough points for a bounding box (need at least 4 points = 8 coordinates)
            return new BoundingBox(0, 0, 0, 0);
        }

        // Polygon is a list of x,y coordinates: [x1, y1, x2, y2, x3, y3, x4, y4]
        // Calculate min/max to get bounding rectangle
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        for (int i = 0; i < polygon.Count; i += 2)
        {
            double x = polygon[i];
            double y = polygon[i + 1];

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        // Normalize coordinates to 0.0 - 1.0 range
        double normalizedX = pageWidth > 0 ? minX / pageWidth : 0;
        double normalizedY = pageHeight > 0 ? minY / pageHeight : 0;
        double normalizedWidth = pageWidth > 0 ? (maxX - minX) / pageWidth : 0;
        double normalizedHeight = pageHeight > 0 ? (maxY - minY) / pageHeight : 0;

        // Clamp values to [0, 1] ranges to handle any edge cases
        normalizedX = Math.Clamp(normalizedX, 0.0, 1.0);
        normalizedY = Math.Clamp(normalizedY, 0.0, 1.0);
        normalizedWidth = Math.Clamp(normalizedWidth, 0.0, 1.0);
        normalizedHeight = Math.Clamp(normalizedHeight, 0.0, 1.0);

        return new BoundingBox(normalizedX, normalizedY, normalizedWidth, normalizedHeight);
    }

    #endregion

    #region Helper Methods

    private static bool IsTableOnPage(DocumentTable table, int pageNumber)
    {
        if (table.BoundingRegions == null || table.BoundingRegions.Count == 0)
        {
            return false;
        }

        return table.BoundingRegions.Any(region => region.PageNumber == pageNumber);
    }

    private static bool IsKeyValuePairOnPage(DocumentKeyValuePair kvp, int pageNumber)
    {
        // Check if key is on this page
        bool keyOnPage = kvp.Key?.BoundingRegions?.Any(r => r.PageNumber == pageNumber) ?? false;
        // Or if value is on this page
        bool valueOnPage = kvp.Value?.BoundingRegions?.Any(r => r.PageNumber == pageNumber) ?? false;

        return keyOnPage || valueOnPage;
    }

    private static double CalculatePageConfidence(DocumentPage page)
    {
        // Calculate confidence based on words
        if (page.Words != null && page.Words.Count > 0)
        {
            return page.Words.Average(w => w.Confidence);
        }

        // Default confidence if no words found
        return 0.95;
    }

    private static double CalculateLineConfidence(DocumentPage page, DocumentLine line)
    {
        // Try to calculate confidence from words that are part of this line
        if (page.Words == null || page.Words.Count == 0)
        {
            return 0.95;
        }

        if (line.Spans == null || line.Spans.Count == 0)
        {
            // If the line has no span information, use page confidence
            return CalculatePageConfidence(page);
        }

        List<DocumentWord> lineWords = page.Words
            .Where(word => IsWordInLine(word.Span, line.Spans))
            .ToList();

        return lineWords.Count != 0 ? lineWords.Average(w => w.Confidence) : 0.95;
    }

    private static bool IsWordInLine(DocumentSpan wordSpan, IReadOnlyList<DocumentSpan> lineSpans)
    {
        return lineSpans.Any(lineSpan => IsSpanWithin(wordSpan, lineSpan));
    }

    private static bool IsSpanWithin(DocumentSpan inner, DocumentSpan outer)
    {
        int innerStart = inner.Offset;
        int innerEnd = inner.Offset + inner.Length;
        int outerStart = outer.Offset;
        int outerEnd = outer.Offset + outer.Length;
        
        return innerStart >= outerStart && innerEnd <= outerEnd;
    }

    private static string? DetectPrimaryLanguage(AnalyzeResult azureResult)
    {
        // Try to detect primary language from pages
        if (azureResult.Languages != null && azureResult.Languages.Count > 0)
        {
            return azureResult.Languages[0].Locale;
        }

        return null;
    }

    #endregion

    #region Logging

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "Starting OCR analysis. DocumentId={DocumentId}, JobId={JobId}, ModelId={ModelId}")]
    private partial void LogOcrAnalysisStarted(Guid documentId, Guid jobId, string modelId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "OCR analysis completed. DocumentId={DocumentId}, JobId={JobId}, PageCount={PageCount}, DurationMs={DurationMs}, AverageConfidence={AverageConfidence:F2}")]
    private partial void LogOcrAnalysisCompleted(Guid documentId, Guid jobId, int pageCount, long durationMs, double averageConfidence);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error,
        Message = "OCR analysis failed. DocumentId={DocumentId}, JobId={JobId}")]
    private partial void LogOcrAnalysisFailed(Exception exception, Guid documentId, Guid jobId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning,
        Message = "OCR analysis cancelled. DocumentId={DocumentId}, JobId={JobId}")]
    private partial void LogOcrAnalysisCancelled(Guid documentId, Guid jobId);

    #endregion
}
