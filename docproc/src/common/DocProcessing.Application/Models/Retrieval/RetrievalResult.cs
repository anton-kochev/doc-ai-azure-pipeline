namespace DocProcessing.Application.Models.Retrieval;

/// <summary>
/// Aggregated result of a retrieval query, containing ranked chunks
/// and execution metadata.
/// </summary>
public sealed record RetrievalResult
{
    /// <summary>
    /// Gets the chunks ranked by descending relevance score.
    /// </summary>
    public required IReadOnlyList<RetrievedChunk> Chunks { get; init; }

    /// <summary>
    /// Gets the query text that was embedded and searched.
    /// </summary>
    public required string QueryText { get; init; }

    /// <summary>
    /// Gets the document ID the search was scoped to.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Gets the total number of chunks before score threshold filtering.
    /// </summary>
    public int TotalCandidates { get; init; }

    /// <summary>
    /// Gets the sum of token counts across all returned chunks.
    /// </summary>
    public int TotalTokens => Chunks.Sum(c => c.TokenCount);

    /// <summary>
    /// Gets the time taken for the vector search (excludes query embedding time).
    /// </summary>
    public TimeSpan SearchDuration { get; init; }

    /// <summary>
    /// Gets the time taken to embed the query text.
    /// </summary>
    public TimeSpan EmbeddingDuration { get; init; }
}
