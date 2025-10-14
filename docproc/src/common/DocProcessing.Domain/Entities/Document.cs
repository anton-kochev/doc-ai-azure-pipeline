using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.Domain.Entities;

[Index(nameof(TenantId), nameof(Status), Name = "IX_Documents_Tenant_Status")]
[Index(nameof(BlobContainer), nameof(BlobPath), Name = "IX_Documents_Blob")]
public sealed class Document
{
    [Key]
    public Guid DocumentId { get; set; }

    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(512)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public long SizeBytes { get; set; }

    [Required]
    [MaxLength(128)]
    public string BlobContainer { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string BlobPath { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? BlobETag { get; set; }

    [MaxLength(32)]
    public byte[]? Sha256Hash { get; set; }

    [Required]
    [MaxLength(256)]
    public string UploadedBy { get; set; } = string.Empty;

    [Required]
    public DateTime UploadedAtUtc { get; set; }

    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    [Column(TypeName = "nvarchar(max)")]
    public string? MetadataJson { get; set; }

    public DateTime? RetentionUntilUtc { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

public enum DocumentStatus
{
    Uploaded,
    Processing,
    Completed,
    Failed,
    Deleted
}
