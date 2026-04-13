namespace DocProcessing.Application.Pipeline.Options;

/// <summary>
/// Configuration options for the RAG retrieval layer.
/// </summary>
public sealed class RetrievalOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Retrieval";

    /// <summary>
    /// Default number of top-k chunks to retrieve when not specified in the query.
    /// </summary>
    public int DefaultTopK { get; set; } = 10;

    /// <summary>
    /// Default minimum similarity score threshold (0.0-1.0).
    /// Chunks scoring below this are discarded.
    /// </summary>
    public double DefaultScoreThreshold { get; set; } = 0.3;

    /// <summary>
    /// Maximum allowed TopK value to prevent excessive result sets.
    /// </summary>
    public int MaxTopK { get; set; } = 50;
}
