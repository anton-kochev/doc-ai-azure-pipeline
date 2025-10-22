using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.Domain.Entities;

/// <summary>
/// Represents a document uploaded to the system for processing.
/// </summary>
[Index(nameof(TenantId), nameof(Status), Name = "IX_Documents_Tenant_Status")]
[Index(nameof(BlobContainer), nameof(BlobPath), Name = "IX_Documents_Blob")]
public sealed class Document
{
    /// <summary>
    /// Gets or sets the unique identifier for the document.
    /// </summary>
    [Key]
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the optional tenant identifier for multi-tenancy support.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the original file name of the uploaded document.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME content type of the document (e.g., "application/pdf").
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the document in bytes.
    /// </summary>
    [Required]
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the Azure Blob Storage container name where the document is stored.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string BlobContainer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the blob path/name within the container.
    /// </summary>
    [Required]
    [MaxLength(1024)]
    public string BlobPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ETag from blob storage for concurrency control.
    /// </summary>
    [MaxLength(128)]
    public string? BlobETag { get; set; }

    /// <summary>
    /// Gets or sets the SHA256 hash of the document content for deduplication and integrity verification.
    /// </summary>
    [MaxLength(32)]
    public byte[]? Sha256Hash { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who uploaded the document.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the document was uploaded.
    /// </summary>
    [Required]
    public DateTime UploadedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the current processing status of the document.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    /// <summary>
    /// Gets or sets optional metadata in JSON format.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the document should be deleted (for retention policies).
    /// </summary>
    public DateTime? RetentionUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency control.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Defines the possible statuses for a document in the processing pipeline.
/// </summary>
public enum DocumentStatus
{
    /// <summary>
    /// The document has been uploaded but not yet processed.
    /// </summary>
    Uploaded,

    /// <summary>
    /// The document is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// The document has been successfully processed.
    /// </summary>
    Completed,

    /// <summary>
    /// Processing of the document has failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The document has been marked as deleted.
    /// </summary>
    Deleted
}
