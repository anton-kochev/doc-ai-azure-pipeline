namespace DocProcessing.Application.Models.Embedding;

/// <summary>
/// Metadata summarising the embedding operation for a document.
/// </summary>
public sealed record EmbedMetadata
{
    /// <summary>
    /// Gets the total number of chunks that were embedded.
    /// </summary>
    public int TotalChunks { get; init; }

    /// <summary>
    /// Gets the name of the embedding model used.
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the dimensionality of the embedding vectors.
    /// </summary>
    public int Dimensions { get; init; }

    /// <summary>
    /// Gets the number of batches used to generate embeddings.
    /// </summary>
    public int BatchCount { get; init; }
}
