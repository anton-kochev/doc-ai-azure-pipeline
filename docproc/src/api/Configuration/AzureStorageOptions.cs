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
    /// Azure Storage connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Default container name for uploads.
    /// </summary>
    public string ContainerName { get; set; } = "uploads";

    /// <summary>
    /// SAS token expiration time in hours.
    /// </summary>
    public int SasExpirationHours { get; set; } = 1;
}
