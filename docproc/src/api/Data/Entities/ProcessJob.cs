using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace api.Data.Entities;

[Index(nameof(DocumentId), nameof(IdempotencyKey), Name = "IX_ProcessJobs_DocumentId_IdempotencyKey", IsUnique = true)]
[Index(nameof(DocumentId), Name = "IX_ProcessJobs_DocumentId")]
[Index(nameof(Status), nameof(Priority), Name = "IX_ProcessJobs_Status_Priority")]
[Index(nameof(CorrelationId), Name = "IX_ProcessJobs_CorrelationId")]
public class ProcessJob
{
    [Key]
    public Guid JobId { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column(TypeName = "varchar(128)")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public ProcessJobStatus Status { get; set; } = ProcessJobStatus.Pending;

    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public ProcessJobStage Stage { get; set; } = ProcessJobStage.Uploaded;

    [Required]
    public int Attempts { get; set; } = 0;

    [MaxLength(64)]
    [Column(TypeName = "nvarchar(64)")]
    public string? LastErrorCode { get; set; }

    [MaxLength(1024)]
    [Column(TypeName = "nvarchar(1024)")]
    public string? LastErrorMessage { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedAtUtc { get; set; }

    [Column(TypeName = "datetime2")]
    public DateTime? StartedAtUtc { get; set; }

    [Column(TypeName = "datetime2")]
    public DateTime? CompletedAtUtc { get; set; }

    [Required]
    [MaxLength(64)]
    [Column(TypeName = "varchar(64)")]
    public string CorrelationId { get; set; } = string.Empty;

    [MaxLength(128)]
    [Column(TypeName = "nvarchar(128)")]
    public string? ExtractionProfile { get; set; }

    [Required]
    public byte Priority { get; set; } = 0;

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation property
    [ForeignKey(nameof(DocumentId))]
    public Document? Document { get; set; }
}

public enum ProcessJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public enum ProcessJobStage
{
    Uploaded,
    OCR,
    Preprocess,
    Embed,
    Extract,
    Validate,
    Persist,
    Notify
}
