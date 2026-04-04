using System.Diagnostics;
using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Chunking;
using DocProcessing.Application.Models.Embedding;
using DocProcessing.Application.Pipeline.Options;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Application.Pipeline;

/// <summary>
/// Embedding stage activity that generates vector embeddings for document chunks
/// and stores them in a vector database for downstream retrieval.
/// </summary>
public sealed partial class EmbedStageActivity : IJobStageActivity
{
    private readonly ILogger<EmbedStageActivity> _logger;
    private readonly IStorageService _storageService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly ChunkingOptions _chunkingOptions;
    private readonly TimeProvider _timeProvider;

    public string StageName => "Embed";
    public ProcessJobStage Stage => ProcessJobStage.Embed;

    public EmbedStageActivity(
        ILogger<EmbedStageActivity> logger,
        IStorageService storageService,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<ChunkingOptions> chunkingOptions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(embeddingService);
        ArgumentNullException.ThrowIfNull(vectorStoreService);
        ArgumentNullException.ThrowIfNull(embeddingOptions);
        ArgumentNullException.ThrowIfNull(chunkingOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _logger = logger;
        _storageService = storageService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _embeddingOptions = embeddingOptions.Value;
        _chunkingOptions = chunkingOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        Stopwatch stopwatch = Stopwatch.StartNew();

        LogEmbedStageStarting(_logger, context.Job.JobId, context.CorrelationId);

        try
        {
            // Extract chunk blob path from metadata
            if (!context.Metadata.TryGetValue(StageMetadataKeys.ChunkBlobPath, out object? chunkBlobPathObj) ||
                chunkBlobPathObj is not string chunkBlobPath)
            {
                LogChunkBlobPathNotFound(_logger, context.Job.JobId, context.CorrelationId);

                return StageResult.Failure(
                    errorCode: "EMBED_MISSING_CHUNK_PATH",
                    errorMessage: "Chunk blob path not found in stage context metadata");
            }

            // Download chunk result from blob storage
            ChunkResult? chunkResult = await _storageService.DownloadJsonAsync<ChunkResult>(
                _chunkingOptions.OutputBlobContainer,
                chunkBlobPath,
                cancellationToken);

            if (chunkResult == null)
            {
                LogChunkResultNotFound(_logger, chunkBlobPath, context.Job.JobId, context.CorrelationId);

                return StageResult.Failure(
                    errorCode: "EMBED_CHUNK_RESULT_NOT_FOUND",
                    errorMessage: $"Chunk result not found at blob path: {chunkBlobPath}");
            }

            // Generate embeddings and build embedded chunks
            List<EmbeddedChunk> allEmbeddedChunks = [];
            int batchCount = 0;

            if (chunkResult.Chunks.Count > 0)
            {
                // Batch chunks by configured batch size
                var batches = chunkResult.Chunks
                    .Chunk(_embeddingOptions.BatchSize)
                    .ToList();

                batchCount = batches.Count;

                foreach (DocumentChunk[] batch in batches)
                {
                    IReadOnlyList<string> texts = batch.Select(c => c.Content).ToList();
                    IReadOnlyList<float[]> embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);

                    for (int i = 0; i < batch.Length; i++)
                    {
                        allEmbeddedChunks.Add(new EmbeddedChunk
                        {
                            ChunkId = batch[i].ChunkId,
                            DocumentId = batch[i].DocumentId,
                            ChunkIndex = batch[i].ChunkIndex,
                            Content = batch[i].Content,
                            ChunkType = batch[i].ChunkType,
                            PageNumbers = batch[i].PageNumbers,
                            TokenCount = batch[i].TokenCount,
                            Embedding = embeddings[i]
                        });
                    }
                }

                // Upsert all embedded chunks to vector store
                await _vectorStoreService.UpsertChunksAsync(allEmbeddedChunks, cancellationToken);
            }

            // Build embed result
            EmbedResult embedResult = new()
            {
                DocumentId = context.Job.DocumentId,
                JobId = context.Job.JobId,
                EmbeddedChunks = allEmbeddedChunks,
                Metadata = new EmbedMetadata
                {
                    TotalChunks = allEmbeddedChunks.Count,
                    ModelName = _embeddingOptions.DeploymentName,
                    Dimensions = _embeddingOptions.Dimensions,
                    BatchCount = batchCount
                },
                ProcessedAt = _timeProvider.GetUtcNow(),
                ProcessingDuration = stopwatch.Elapsed
            };

            // Get tenant ID from metadata
            string tenantId;
            if (context.Metadata.TryGetValue(StageMetadataKeys.TenantId, out object? tenantIdObj))
            {
                tenantId = tenantIdObj.ToString() ?? "default";
            }
            else
            {
                tenantId = "default";
            }

            // Upload embed result to blob storage
            string blobPath = $"{tenantId}/{context.Job.DocumentId}/embed-result.json";

            string uploadedBlobPath;
            try
            {
                uploadedBlobPath = await _storageService.UploadJsonAsync(
                    _embeddingOptions.OutputBlobContainer,
                    blobPath,
                    embedResult,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogEmbedUploadFailed(_logger, ex, context.Job.JobId, context.CorrelationId);

                return StageResult.Failure(
                    errorCode: "EMBED_UPLOAD_ERROR",
                    errorMessage: "Failed to upload embed result to blob storage.");
            }

            stopwatch.Stop();

            LogEmbedStageCompleted(
                _logger,
                context.Job.JobId,
                allEmbeddedChunks.Count,
                batchCount,
                stopwatch.ElapsedMilliseconds);

            return StageResult.Success(
                output: null,
                metadata: new Dictionary<string, object>
                {
                    [StageMetadataKeys.EmbedBlobPath] = uploadedBlobPath,
                    [StageMetadataKeys.EmbeddedChunks] = allEmbeddedChunks.Count,
                    [StageMetadataKeys.EmbeddingModel] = _embeddingOptions.DeploymentName,
                    [StageMetadataKeys.EmbeddingDimensions] = _embeddingOptions.Dimensions,
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

            LogEmbedStageFailed(_logger, ex, context.Job.JobId, context.CorrelationId);

            return StageResult.Failure(
                errorCode: "EMBED_ERROR",
                errorMessage: "An unexpected error occurred during the embedding stage.");
        }
    }

    #region Logging

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting Embed stage for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogEmbedStageStarting(
        ILogger logger,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Chunk blob path not found in metadata. JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogChunkBlobPathNotFound(
        ILogger logger,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Chunk result not found at path: {ChunkBlobPath}. JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogChunkResultNotFound(
        ILogger logger,
        string chunkBlobPath,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Embed stage completed for JobId: {JobId}. EmbeddedChunks: {EmbeddedChunks}, Batches: {BatchCount}, Duration: {DurationMs}ms")]
    private static partial void LogEmbedStageCompleted(
        ILogger logger,
        Guid jobId,
        int embeddedChunks,
        int batchCount,
        long durationMs);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Embed stage failed for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogEmbedStageFailed(
        ILogger logger,
        Exception exception,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Failed to upload embed result for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogEmbedUploadFailed(
        ILogger logger,
        Exception exception,
        Guid jobId,
        string correlationId);

    #endregion
}
