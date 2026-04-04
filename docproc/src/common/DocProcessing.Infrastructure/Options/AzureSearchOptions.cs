namespace DocProcessing.Infrastructure.Options;

/// <summary>
/// Configuration options for the Azure AI Search vector store.
/// </summary>
public sealed class AzureSearchOptions
{
    /// <summary>
    /// Gets or sets the Azure AI Search service endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the search index name for document chunk embeddings.
    /// </summary>
    public string IndexName { get; set; } = "docproc-chunks";

    /// <summary>
    /// Gets or sets the dimensionality of the embedding vectors for the search index.
    /// </summary>
    public int Dimensions { get; set; } = 1536;
}
