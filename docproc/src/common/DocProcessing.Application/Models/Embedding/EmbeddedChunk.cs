using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Models.Embedding;

/// <summary>
/// Represents a document chunk paired with its embedding vector,
/// ready for storage in a vector database.
/// </summary>
public sealed record EmbeddedChunk
{
    /// <summary>
    /// Gets the stable, unique identifier for the source chunk.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Gets the document this chunk belongs to.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the zero-based sequential position of this chunk within the document.
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Gets the text content of the chunk.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the origin content type of this chunk.
    /// </summary>
    public ChunkType ChunkType { get; init; }

    /// <summary>
    /// Gets the one-based page numbers that this chunk spans.
    /// </summary>
    public IReadOnlyList<int> PageNumbers { get; init; } = [];

    /// <summary>
    /// Gets the estimated token count for this chunk's content.
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Gets the embedding vector for this chunk's content.
    /// </summary>
    public required float[] Embedding { get; init; }
}
