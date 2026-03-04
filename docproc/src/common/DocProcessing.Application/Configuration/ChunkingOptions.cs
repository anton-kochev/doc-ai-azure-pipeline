namespace DocProcessing.Application.Configuration;

/// <summary>
/// Configuration options for the chunking stage.
/// </summary>
public sealed class ChunkingOptions
{
    /// <summary>
    /// Gets or sets the blob storage container name for chunk results.
    /// </summary>
    public string OutputBlobContainer { get; init; } = "chunk-results";

    /// <summary>
    /// Gets or sets the maximum number of tokens per chunk.
    /// </summary>
    public int MaxChunkSize { get; init; } = 512;

    /// <summary>
    /// Gets or sets the number of tokens that adjacent chunks share as overlap
    /// to preserve context across chunk boundaries.
    /// </summary>
    public int OverlapTokens { get; init; } = 50;

    /// <summary>
    /// Gets or sets the multiplier used to estimate token count from character count
    /// (characters / <see cref="TokenEstimationFactor"/> ≈ tokens).
    /// </summary>
    public double TokenEstimationFactor { get; init; } = 1.3;
}
