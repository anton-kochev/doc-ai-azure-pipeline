using System.ComponentModel.DataAnnotations;

namespace DocProcessing.Application.Models;

/// <summary>
/// Message contract for document processing requests from Service Bus.
///
/// Required fields (validated by MessageValidator):
/// - Version: Message schema version (defaults to "1.0")
/// - JobId: Identifier to retrieve job details from database
/// - CorrelationId: For distributed tracing
///
/// Optional fields (retrieved from database during orchestration):
/// - DocumentId, TenantId, BlobContainer, BlobPath, ExtractionProfile, etc.
/// </summary>
public sealed class ProcessDocumentMessage
{
    /// <summary>
    /// Version of the message schema for compatibility tracking.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Unique identifier for the background job.
    /// Used to retrieve ProcessJob and Document details from database.
    /// </summary>
    [Required]
    public required string JobId { get; set; }

    /// <summary>
    /// Correlation identifier for tracking requests across services.
    /// </summary>
    [Required]
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Unique identifier for the document to be processed.
    /// Optional - can be retrieved from database using JobId.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenancy support.
    /// Optional - can be retrieved from database using JobId.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure Blob Storage container name where the document is stored.
    /// Optional - can be retrieved from database using JobId.
    /// </summary>
    public string? BlobContainer { get; set; }

    /// <summary>
    /// Full path to the blob within the container.
    /// Optional - can be retrieved from database using JobId.
    /// </summary>
    public string? BlobPath { get; set; }

    /// <summary>
    /// Idempotency key to prevent duplicate processing.
    /// Optional - can be retrieved from database using JobId.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// UTC timestamp when the message was enqueued.
    /// Optional timestamp for tracking.
    /// </summary>
    public DateTime? EnqueuedAtUtc { get; set; }

    /// <summary>
    /// Extraction profile name or configuration identifier.
    /// Optional - can be retrieved from database using JobId.
    /// </summary>
    public string? ExtractionProfile { get; set; }
}
