namespace DocProcessing.Application.Pipeline.Options;

/// <summary>
/// Configuration options for the embedding pipeline stage.
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// Gets or sets the OpenAI provider. Supported values: "Azure", "OpenAI".
    /// </summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>
    /// Gets or sets the model or deployment name for the embedding model.
    /// For Azure OpenAI this is the deployment name; for OpenAI this is the model ID.
    /// </summary>
    public string DeploymentName { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Gets or sets the Azure OpenAI endpoint URL. Required when Provider is "Azure".
    /// </summary>
    public string? AzureEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the OpenAI API key. Required when Provider is "OpenAI".
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the dimensionality of the embedding vectors.
    /// </summary>
    public int Dimensions { get; set; } = 1536;

    /// <summary>
    /// Gets or sets the maximum number of chunks to embed in a single API call.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the blob storage container name for embedding results.
    /// </summary>
    public string OutputBlobContainer { get; set; } = "embed-results";
}
