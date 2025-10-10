namespace Api.Configuration;

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
    /// Azure Storage account name.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Default container name for uploads.
    /// </summary>
    public string ContainerName { get; set; } = "uploads";
}
