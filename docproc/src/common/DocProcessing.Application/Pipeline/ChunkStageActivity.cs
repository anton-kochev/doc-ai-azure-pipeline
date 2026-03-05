using System.Diagnostics;
using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Chunking;
using DocProcessing.Application.Models.Preprocessing;
using DocProcessing.Application.Services.Chunking;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Application.Pipeline;

/// <summary>
/// Chunking stage activity that splits preprocessed document text into discrete chunks
/// ready for downstream embedding and extraction.
/// </summary>
public sealed partial class ChunkStageActivity : IJobStageActivity
{
    private readonly ILogger<ChunkStageActivity> _logger;
    private readonly IStorageService _storageService;
    private readonly ChunkingOptions _options;
    private readonly PreprocessOptions _preprocessOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IDocumentChunker _documentChunker;

    public string StageName => "Chunk";
    public ProcessJobStage Stage => ProcessJobStage.Chunk;

    public ChunkStageActivity(
        ILogger<ChunkStageActivity> logger,
        IStorageService storageService,
        IOptions<ChunkingOptions> options,
        IOptions<PreprocessOptions> preprocessOptions,
        TimeProvider timeProvider,
        IDocumentChunker documentChunker)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(preprocessOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(documentChunker);

        _logger = logger;
        _storageService = storageService;
        _options = options.Value;
        _preprocessOptions = preprocessOptions.Value;
        _timeProvider = timeProvider;
        _documentChunker = documentChunker;
    }

    public async Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        Stopwatch stopwatch = Stopwatch.StartNew();

        LogChunkStageStarting(_logger, context.Job.JobId, context.CorrelationId);

        try
        {
            // Extract preprocess blob path from metadata
            if (!context.Metadata.TryGetValue(StageMetadataKeys.PreprocessBlobPath, out object? preprocessBlobPathObj) ||
                preprocessBlobPathObj is not string preprocessBlobPath)
            {
                LogPreprocessBlobPathNotFound(_logger, context.Job.JobId, context.CorrelationId);

                return StageResult.Failure(
                    errorCode: "CHUNK_MISSING_PREPROCESS_PATH",
                    errorMessage: "Preprocess blob path not found in stage context metadata");
            }

            // Download preprocess result from blob storage
            PreprocessResult? preprocessResult = await _storageService.DownloadJsonAsync<PreprocessResult>(
                _preprocessOptions.OutputBlobContainer,
                preprocessBlobPath,
                cancellationToken);

            if (preprocessResult == null)
            {
                LogPreprocessResultNotFound(_logger, preprocessBlobPath, context.Job.JobId, context.CorrelationId);

                return StageResult.Failure(
                    errorCode: "CHUNK_PREPROCESS_NOT_FOUND",
                    errorMessage: $"Preprocess result not found at blob path: {preprocessBlobPath}");
            }

            // Chunk the preprocessed document
            var (chunks, chunkMetadata) = _documentChunker.ChunkDocument(preprocessResult, _options);

            // Get tenant ID from metadata
            string tenantId;
            if (context.Metadata.TryGetValue(StageMetadataKeys.TenantId, out object? tenantIdObj))
            {
                tenantId = tenantIdObj.ToString() ?? "default";
            }
            else
            {
                LogTenantIdNotFound(_logger, context.Job.JobId, context.CorrelationId);
                tenantId = "default";
            }

            // Build chunk result
            ChunkResult chunkResult = new ChunkResult
            {
                DocumentId = context.Job.DocumentId,
                JobId = context.Job.JobId,
                Chunks = chunks,
                Metadata = chunkMetadata,
                ProcessedAt = _timeProvider.GetUtcNow(),
                ProcessingDuration = stopwatch.Elapsed
            };

            // Upload chunk result to blob storage
            string blobPath = $"{tenantId}/{context.Job.DocumentId}/chunk-result.json";
            string uploadedBlobPath = await _storageService.UploadJsonAsync(
                _options.OutputBlobContainer,
                blobPath,
                chunkResult,
                cancellationToken);

            stopwatch.Stop();

            LogChunkStageCompleted(
                _logger,
                context.Job.JobId,
                chunkResult.Chunks.Count,
                chunkMetadata.TotalTokens,
                stopwatch.ElapsedMilliseconds);

            return StageResult.Success(
                output: null,
                metadata: new Dictionary<string, object>
                {
                    [StageMetadataKeys.ChunkBlobPath] = uploadedBlobPath,
                    [StageMetadataKeys.TotalChunks] = chunkResult.Chunks.Count,
                    [StageMetadataKeys.TextChunks] = chunkMetadata.TextChunks,
                    [StageMetadataKeys.TableChunks] = chunkMetadata.TableChunks,
                    [StageMetadataKeys.TotalTokens] = chunkMetadata.TotalTokens,
                    [StageMetadataKeys.ProcessingDurationMs] = stopwatch.ElapsedMilliseconds
                });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            LogChunkStageFailed(_logger, ex, context.Job.JobId, context.CorrelationId);

            return StageResult.Failure(
                errorCode: "CHUNK_ERROR",
                errorMessage: "An unexpected error occurred during the chunking stage.");
        }
    }

    #region Logging

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting Chunk stage for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogChunkStageStarting(
        ILogger logger,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Preprocess blob path not found in metadata. JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogPreprocessBlobPathNotFound(
        ILogger logger,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Preprocess result not found at path: {PreprocessBlobPath}. JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogPreprocessResultNotFound(
        ILogger logger,
        string preprocessBlobPath,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Chunk stage completed for JobId: {JobId}. TotalChunks: {TotalChunks}, TotalTokens: {TotalTokens}, Duration: {DurationMs}ms")]
    private static partial void LogChunkStageCompleted(
        ILogger logger,
        Guid jobId,
        int totalChunks,
        int totalTokens,
        long durationMs);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Chunk stage failed for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogChunkStageFailed(
        ILogger logger,
        Exception exception,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "TenantId not found in metadata, using default. JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogTenantIdNotFound(
        ILogger logger,
        Guid jobId,
        string correlationId);

    #endregion
}
