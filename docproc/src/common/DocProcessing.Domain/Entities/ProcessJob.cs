using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.Domain.Entities;

/// <summary>
/// Represents a processing job for a document, tracking the workflow through various stages.
/// Implements idempotency to prevent duplicate processing of the same document.
/// </summary>
[Index(nameof(DocumentId), nameof(IdempotencyKey), Name = "IX_ProcessJobs_DocumentId_IdempotencyKey", IsUnique = true)]
[Index(nameof(DocumentId), Name = "IX_ProcessJobs_DocumentId")]
[Index(nameof(Status), nameof(Priority), Name = "IX_ProcessJobs_Status_Priority")]
[Index(nameof(CorrelationId), Name = "IX_ProcessJobs_CorrelationId")]
public sealed class ProcessJob
{
    /// <summary>
    /// Gets or sets the unique identifier for the processing job.
    /// </summary>
    [Key]
    public Guid JobId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the document being processed.
    /// </summary>
    [Required]
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key to prevent duplicate processing of the same document.
    /// Computed from tenant ID, document hash, and extraction profile.
    /// </summary>
    [Required]
    [MaxLength(128)]
    [Column(TypeName = "varchar(128)")]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the processing job.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public ProcessJobStatus Status { get; set; } = ProcessJobStatus.Pending;

    /// <summary>
    /// Gets or sets the current processing stage within the workflow.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public ProcessJobStage Stage { get; set; } = ProcessJobStage.Uploaded;

    /// <summary>
    /// Gets or sets the number of processing attempts for this job.
    /// </summary>
    [Required]
    public int Attempts { get; set; } = 0;

    /// <summary>
    /// Gets or sets the error code from the most recent failure, if any.
    /// </summary>
    [MaxLength(64)]
    [Column(TypeName = "nvarchar(64)")]
    public string? LastErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message from the most recent failure, if any.
    /// </summary>
    [MaxLength(1024)]
    [Column(TypeName = "nvarchar(1024)")]
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the job was created.
    /// </summary>
    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when processing started.
    /// </summary>
    [Column(TypeName = "datetime2")]
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when processing completed (successfully or with failure).
    /// </summary>
    [Column(TypeName = "datetime2")]
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing and logging.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column(TypeName = "varchar(64)")]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional extraction profile name to use for processing.
    /// </summary>
    [MaxLength(128)]
    [Column(TypeName = "nvarchar(128)")]
    public string? ExtractionProfile { get; set; }

    /// <summary>
    /// Gets or sets the job priority (0-255, higher value = higher priority).
    /// </summary>
    [Required]
    public byte Priority { get; set; } = 0;

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency control.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the associated document.
    /// </summary>
    [ForeignKey(nameof(DocumentId))]
    public Document? Document { get; set; }
}

/// <summary>
/// Defines the possible statuses for a processing job.
/// </summary>
public enum ProcessJobStatus
{
    /// <summary>
    /// The job is waiting to be processed.
    /// </summary>
    Pending,

    /// <summary>
    /// The job is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// The job has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The job has failed and requires attention.
    /// </summary>
    Failed,

    /// <summary>
    /// The job requires manual review before continuing.
    /// </summary>
    ManualReview,
}

/// <summary>
/// Defines the processing stages in the document processing pipeline.
/// </summary>
public enum ProcessJobStage
{
    /// <summary>
    /// Document has been uploaded to storage.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Optical Character Recognition (OCR) stage.
    /// </summary>
    OCR,

    /// <summary>
    /// Document preprocessing and normalization stage.
    /// </summary>
    Preprocess,

    /// <summary>
    /// Document embedding generation stage.
    /// </summary>
    Embed,

    /// <summary>
    /// Information extraction stage.
    /// </summary>
    Extract,

    /// <summary>
    /// Extracted data validation stage.
    /// </summary>
    Validate,

    /// <summary>
    /// Data persistence stage.
    /// </summary>
    Persist,

    /// <summary>
    /// Notification and completion stage.
    /// </summary>
    Notify
}
