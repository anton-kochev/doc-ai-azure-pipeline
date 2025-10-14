namespace DocProcessing.Api.Configuration;

/// <summary>
/// Configuration options for file upload validation.
/// </summary>
public class FileUploadOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "FileUpload";

    /// <summary>
    /// List of allowed MIME types (e.g., "application/pdf", "image/jpeg").
    /// </summary>
    public string[] AllowedFileTypes { get; set; } = [];

    /// <summary>
    /// Maximum allowed file size in megabytes.
    /// </summary>
    public long MaxFileSizeMB { get; set; } = 10;
}
