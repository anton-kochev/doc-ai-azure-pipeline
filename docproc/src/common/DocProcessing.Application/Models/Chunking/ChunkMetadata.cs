namespace DocProcessing.Application.Models.Chunking;

/// <summary>
/// Statistical summary of the chunks produced during the chunking stage.
/// </summary>
public sealed record ChunkMetadata
{
    /// <summary>
    /// Gets the total number of chunks produced.
    /// </summary>
    public int TotalChunks { get; init; }

    /// <summary>
    /// Gets the number of chunks that originated from plain text content.
    /// </summary>
    public int TextChunks { get; init; }

    /// <summary>
    /// Gets the number of chunks that originated from structured tables.
    /// </summary>
    public int TableChunks { get; init; }

    /// <summary>
    /// Gets the number of chunks that originated from form fields.
    /// </summary>
    public int FormFieldChunks { get; init; }

    /// <summary>
    /// Gets the sum of estimated token counts across all chunks.
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Gets the maximum chunk size (in tokens) that was configured for this run.
    /// </summary>
    public int MaxChunkSize { get; init; }

    /// <summary>
    /// Gets the overlap size (in tokens) that was configured for this run.
    /// </summary>
    public int OverlapTokens { get; init; }
}
