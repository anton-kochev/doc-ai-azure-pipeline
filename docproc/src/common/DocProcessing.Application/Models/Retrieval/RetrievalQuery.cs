using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Models.Retrieval;

/// <summary>
/// Describes a vector similarity search request against the chunk store.
/// </summary>
public sealed record RetrievalQuery
{
    /// <summary>
    /// Gets the natural-language query text to embed and search for.
    /// </summary>
    public required string QueryText { get; init; }

    /// <summary>
    /// Gets the document to restrict results to.
    /// Required for pipeline usage — we always search within a single document.
    /// </summary>
    public required Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the maximum number of chunks to return.
    /// Falls back to <see cref="Pipeline.Options.RetrievalOptions.DefaultTopK"/> when null.
    /// </summary>
    public int? TopK { get; init; }

    /// <summary>
    /// Gets the minimum similarity score (0.0-1.0).
    /// Chunks below this threshold are excluded.
    /// Falls back to <see cref="Pipeline.Options.RetrievalOptions.DefaultScoreThreshold"/> when null.
    /// </summary>
    public double? ScoreThreshold { get; init; }

    /// <summary>
    /// Gets an optional filter to restrict results to specific chunk types.
    /// When null or empty, all chunk types are included.
    /// </summary>
    public IReadOnlyList<ChunkType>? ChunkTypeFilter { get; init; }
}
