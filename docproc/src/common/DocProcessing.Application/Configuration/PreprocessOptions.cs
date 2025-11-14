namespace DocProcessing.Application.Configuration;

/// <summary>
/// Configuration options for the preprocessing stage.
/// </summary>
public sealed class PreprocessOptions
{
    /// <summary>
    /// Gets or sets the blob storage container name for preprocessed results.
    /// </summary>
    public required string OutputBlobContainer { get; init; } = "preprocess-results";

    /// <summary>
    /// Gets or sets whether to enable Unicode normalization (NFC).
    /// </summary>
    public bool EnableUnicodeNormalization { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to enable whitespace cleanup.
    /// </summary>
    public bool EnableWhitespaceCleanup { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to convert tables to structured format (JSON/CSV).
    /// </summary>
    public bool ConvertTablesToStructured { get; init; } = true;

    /// <summary>
    /// Gets or sets the maximum chunk size in tokens for future chunking operations.
    /// </summary>
    public int MaxChunkSize { get; init; } = 512;
}
