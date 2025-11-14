namespace DocProcessing.Application.Models.Preprocessing;

/// <summary>
/// Metadata about the preprocessing operation.
/// </summary>
public sealed class PreprocessMetadata
{
    /// <summary>
    /// Gets or sets the timestamp when preprocessing was performed.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; init; }

    /// <summary>
    /// Gets or sets the duration of the preprocessing operation.
    /// </summary>
    public TimeSpan ProcessingDuration { get; init; }

    /// <summary>
    /// Gets or sets the total number of pages processed.
    /// </summary>
    public int PageCount { get; init; }

    /// <summary>
    /// Gets or sets the total word count across all pages.
    /// </summary>
    public int TotalWordCount { get; init; }

    /// <summary>
    /// Gets or sets the total number of tables processed.
    /// </summary>
    public int TotalTables { get; init; }

    /// <summary>
    /// Gets or sets the total number of form fields processed.
    /// </summary>
    public int TotalFormFields { get; init; }

    /// <summary>
    /// Gets or sets the primary language detected.
    /// </summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>
    /// Gets or sets the normalization settings that were applied.
    /// </summary>
    public Dictionary<string, bool> NormalizationSettings { get; init; } = [];

    /// <summary>
    /// Gets or sets any warnings that occurred during preprocessing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
