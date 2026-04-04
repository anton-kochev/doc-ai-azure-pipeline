namespace DocProcessing.Infrastructure.Options;

/// <summary>
/// Configuration options for the vector store provider selection.
/// </summary>
public sealed class VectorStoreOptions
{
    public const string SectionName = "VectorStore";

    /// <summary>
    /// Gets or sets the active vector store provider.
    /// Supported values: "pgvector", "AzureSearch".
    /// </summary>
    public string Provider { get; set; } = "pgvector";

    /// <summary>
    /// Gets or sets pgvector-specific options.
    /// </summary>
    public PgVectorOptions PgVector { get; set; } = new();

    /// <summary>
    /// Gets or sets Azure AI Search-specific options.
    /// </summary>
    public AzureSearchOptions AzureSearch { get; set; } = new();
}
