namespace DocProcessing.Infrastructure.Storage;

/// <summary>
/// Configuration options for Azure Blob Storage.
/// </summary>
public class AzureStorageOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "AzureStorage";

    /// <summary>
    /// Azure Storage connection string. If provided, this will be used instead of AccountName with managed identity.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Azure Storage account name. Used only when ConnectionString is not provided.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Default container name for uploads.
    /// </summary>
    public string ContainerName { get; set; } = "uploads";

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Delay between retry attempts in seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Maximum retry delay in seconds (for exponential backoff).
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 30;
}
