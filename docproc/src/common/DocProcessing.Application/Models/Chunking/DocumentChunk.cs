using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Models.Chunking;

/// <summary>
/// Represents a single chunk of content produced by the chunking stage,
/// ready for downstream embedding and extraction.
/// </summary>
public sealed record DocumentChunk
{
    /// <summary>
    /// Gets the stable, unique identifier for this chunk.
    /// </summary>
    public string ChunkId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the zero-based sequential position of this chunk within the document.
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Gets the document this chunk belongs to.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the one-based page numbers that this chunk spans.
    /// </summary>
    public IReadOnlyList<int> PageNumbers { get; init; } = [];

    /// <summary>
    /// Gets the inclusive character offset in the full document text where this chunk starts.
    /// Null for non-text chunk types.
    /// </summary>
    public int? StartOffset { get; init; }

    /// <summary>
    /// Gets the exclusive character offset in the full document text where this chunk ends.
    /// Null for non-text chunk types.
    /// </summary>
    public int? EndOffset { get; init; }

    /// <summary>
    /// Gets the estimated token count for this chunk's content.
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Gets the origin content type of this chunk.
    /// </summary>
    public ChunkType ChunkType { get; init; }

    /// <summary>
    /// Gets the text content of this chunk.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the zero-based indexes of the source <c>NormalizedTextBlock</c>s
    /// that contributed to this chunk. Null for non-text chunk types.
    /// </summary>
    public IReadOnlyList<int>? SourceBlocks { get; init; }
}
