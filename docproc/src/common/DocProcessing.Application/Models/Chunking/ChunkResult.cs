namespace DocProcessing.Application.Models.Chunking;

/// <summary>
/// Represents the complete output of the chunking stage for a single document.
/// </summary>
public sealed record ChunkResult
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
    /// Gets the ordered list of chunks produced from the document.
    /// </summary>
    public IReadOnlyList<DocumentChunk> Chunks { get; init; } = [];

    /// <summary>
    /// Gets metadata summarising the chunking operation.
    /// </summary>
    public required ChunkMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when chunking completed.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; init; }

    /// <summary>
    /// Gets the wall-clock duration of the chunking operation.
    /// </summary>
    public TimeSpan ProcessingDuration { get; init; }
}
