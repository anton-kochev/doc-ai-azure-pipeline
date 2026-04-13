using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Models.Retrieval;

/// <summary>
/// A chunk returned from vector similarity search, enriched with
/// relevance score and source citation metadata.
/// </summary>
public sealed record RetrievedChunk
{
    /// <summary>
    /// Gets the stable chunk identifier.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Gets the document this chunk belongs to.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the zero-based position within the document.
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Gets the text content of the chunk.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the content origin type (Text, Table, FormField).
    /// </summary>
    public ChunkType ChunkType { get; init; }

    /// <summary>
    /// Gets the one-based page numbers this chunk spans.
    /// </summary>
    public IReadOnlyList<int> PageNumbers { get; init; } = [];

    /// <summary>
    /// Gets the estimated token count for this chunk.
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Gets the cosine similarity score (0.0-1.0, higher = more relevant).
    /// All providers normalize to cosine similarity at the provider boundary.
    /// </summary>
    public double Score { get; init; }
}
