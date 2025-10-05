namespace Api.Configuration;

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
    /// List of allowed file extensions (e.g., ".pdf", ".docx").
    /// </summary>
    public string[] AllowedFileTypes { get; set; } = [];

    /// <summary>
    /// Maximum allowed file size in megabytes.
    /// </summary>
    public long MaxFileSizeMB { get; set; } = 10;
}
