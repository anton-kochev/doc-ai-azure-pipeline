using System.ComponentModel.DataAnnotations;

namespace DocProcessing.Application.Models;

/// <summary>
/// Message contract for document processing requests from Service Bus.
/// </summary>
public sealed class ProcessDocumentMessage
{
    /// <summary>
    /// Version of the message schema for compatibility tracking.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Unique identifier for the background job.
    /// </summary>
    [Required]
    public required string JobId { get; set; }

    /// <summary>
    /// Unique identifier for the document to be processed.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenancy support.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Correlation identifier for tracking requests across services.
    /// </summary>
    [Required]
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Azure Blob Storage container name where the document is stored.
    /// </summary>
    public string? BlobContainer { get; set; }

    /// <summary>
    /// Full path to the blob within the container.
    /// </summary>
    public string? BlobPath { get; set; }

    /// <summary>
    /// Idempotency key to prevent duplicate processing.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// UTC timestamp when the message was enqueued.
    /// </summary>
    public DateTime? EnqueuedAtUtc { get; set; }

    /// <summary>
    /// Extraction profile name or configuration identifier.
    /// </summary>
    public string? ExtractionProfile { get; set; }
}
