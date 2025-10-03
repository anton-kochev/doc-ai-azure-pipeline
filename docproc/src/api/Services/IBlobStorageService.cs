namespace Api.Services;

/// <summary>
/// Service for managing Azure Blob Storage operations.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Generates a SAS URL for uploading a blob.
    /// </summary>
    /// <param name="fileName">The name of the file to upload.</param>
    /// <param name="contentType">Optional content type of the file.</param>
    /// <returns>Result containing the SAS URL and metadata.</returns>
    Task<SasUrlResult> GenerateSasUrlAsync(string fileName, string? contentType = null);
}

/// <summary>
/// Result of SAS URL generation.
/// </summary>
public record SasUrlResult(
    string SasUrl,
    DateTimeOffset ExpiresOn,
    string FileName,
    string? ContentType
);
