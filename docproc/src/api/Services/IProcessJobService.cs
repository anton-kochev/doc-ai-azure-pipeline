using api.Data.Entities;

namespace Api.Services;

/// <summary>
/// Service for managing ProcessJob entities and job orchestration.
/// </summary>
public interface IProcessJobService
{
    /// <summary>
    /// Gets an existing non-terminal job by idempotency key, or creates a new job if none exists.
    /// </summary>
    /// <param name="documentId">The document ID associated with the job.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <param name="sha256Hash">The SHA256 hash of the document.</param>
    /// <param name="extractionProfile">Optional extraction profile name.</param>
    /// <param name="correlationId">Optional correlation ID for tracking.</param>
    /// <param name="priority">Job priority (0-255, default 0).</param>
    /// <returns>A tuple containing the job ID and a boolean indicating whether it was newly created (true) or already existed (false).</returns>
    Task<(Guid JobId, bool IsNew)> GetOrCreateJobAsync(
        Guid documentId,
        Guid? tenantId,
        byte[] sha256Hash,
        string? extractionProfile = null,
        string? correlationId = null,
        byte priority = 0);

    /// <summary>
    /// Computes the idempotency key for a job based on tenant, document hash, and extraction profile.
    /// </summary>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <param name="sha256Hash">The SHA256 hash of the document.</param>
    /// <param name="extractionProfile">Optional extraction profile name.</param>
    /// <returns>A string representing the idempotency key.</returns>
    string ComputeIdempotencyKey(Guid? tenantId, byte[] sha256Hash, string? extractionProfile);
}
