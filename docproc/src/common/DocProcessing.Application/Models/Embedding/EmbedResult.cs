namespace DocProcessing.Application.Models.Embedding;

/// <summary>
/// Represents the complete output of the embedding stage for a single document.
/// </summary>
public sealed record EmbedResult
{
    /// <summary>
    /// Gets the document identifier.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the processing job identifier.
    /// </summary>
    public Guid JobId { get; init; }

    /// <summary>
    /// Gets the list of chunks with their embedding vectors.
    /// </summary>
    public IReadOnlyList<EmbeddedChunk> EmbeddedChunks { get; init; } = [];

    /// <summary>
    /// Gets metadata summarising the embedding operation.
    /// </summary>
    public required EmbedMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when embedding completed.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; init; }

    /// <summary>
    /// Gets the wall-clock duration of the embedding operation.
    /// </summary>
    public TimeSpan ProcessingDuration { get; init; }
}
