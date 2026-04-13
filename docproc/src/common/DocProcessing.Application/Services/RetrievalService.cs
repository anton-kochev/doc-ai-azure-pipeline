using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Application.Pipeline.Options;
using DocProcessing.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Application.Services;

/// <summary>
/// Orchestrates RAG retrieval: embeds the query, searches the vector store,
/// and applies score thresholds.
/// </summary>
public sealed partial class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly RetrievalOptions _options;
    private readonly ILogger<RetrievalService> _logger;
    private readonly TimeProvider _timeProvider;

    public RetrievalService(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IOptions<RetrievalOptions> options,
        ILogger<RetrievalService> logger,
        TimeProvider timeProvider)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<RetrievalResult> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.QueryText);

        if (query.DocumentId == Guid.Empty)
        {
            throw new ArgumentException("DocumentId must not be empty.", nameof(query));
        }

        if (query.TopK is <= 0)
        {
            throw new ArgumentException("TopK must be greater than zero.", nameof(query));
        }

        int topK = Math.Min(query.TopK ?? _options.DefaultTopK, _options.MaxTopK);
        double threshold = query.ScoreThreshold ?? _options.DefaultScoreThreshold;

        LogRetrievalStarted(query.DocumentId, query.QueryText.Length);

        try
        {
            // Embed query
            long embedStart = _timeProvider.GetTimestamp();
            IReadOnlyList<float[]> embeddings = await _embeddingService
                .GenerateEmbeddingsAsync([query.QueryText], cancellationToken);
            TimeSpan embeddingDuration = _timeProvider.GetElapsedTime(embedStart);

            float[] queryEmbedding = embeddings[0];

            // Vector search
            long searchStart = _timeProvider.GetTimestamp();
            IReadOnlyList<RetrievedChunk> candidates = await _vectorStoreService
                .SearchAsync(queryEmbedding, query.DocumentId, topK,
                    query.ChunkTypeFilter, cancellationToken);
            TimeSpan searchDuration = _timeProvider.GetElapsedTime(searchStart);

            // Score threshold filter (inclusive: >=)
            int totalCandidates = candidates.Count;
            IReadOnlyList<RetrievedChunk> filtered = candidates
                .Where(c => c.Score >= threshold)
                .ToList();

            LogRetrievalCompleted(query.DocumentId, filtered.Count, totalCandidates,
                embeddingDuration.TotalMilliseconds + searchDuration.TotalMilliseconds);

            return new RetrievalResult
            {
                Chunks = filtered,
                QueryText = query.QueryText,
                DocumentId = query.DocumentId,
                TotalCandidates = totalCandidates,
                SearchDuration = searchDuration,
                EmbeddingDuration = embeddingDuration
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRetrievalFailed(ex, query.DocumentId);
            throw new RetrievalFailedException(
                query.DocumentId,
                query.QueryText,
                $"Retrieval failed for document {query.DocumentId}: {ex.Message}",
                ex);
        }
    }

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Starting retrieval for DocumentId={DocumentId}, QueryLength={QueryLength}")]
    private partial void LogRetrievalStarted(Guid documentId, int queryLength);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "Retrieval completed for DocumentId={DocumentId}. ReturnedChunks={ReturnedChunks}, TotalCandidates={TotalCandidates}, DurationMs={DurationMs}")]
    private partial void LogRetrievalCompleted(Guid documentId, int returnedChunks, int totalCandidates, double durationMs);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Error,
        Message = "Retrieval failed for DocumentId={DocumentId}")]
    private partial void LogRetrievalFailed(Exception exception, Guid documentId);
}
