using DocProcessing.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Api.Services;

/// <summary>
/// Implementation of process job management operations.
/// </summary>
public sealed class ProcessJobService : IProcessJobService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ProcessJobService> _logger;
    private readonly TimeProvider _timeProvider;

    public ProcessJobService(AppDbContext dbContext, ILogger<ProcessJobService> logger, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public string ComputeIdempotencyKey(Guid? tenantId, byte[] sha256Hash, string? extractionProfile)
    {
        // Combine tenant ID, document hash, and extraction profile to create idempotency key
        StringBuilder sb = new();
        sb.Append(tenantId?.ToString() ?? "default");
        sb.Append('|');
        sb.Append(Convert.ToHexString(sha256Hash));
        sb.Append('|');
        sb.Append(extractionProfile ?? "default");

        string combined = sb.ToString();

        // Hash the combined string to create a consistent, fixed-length key
        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));

        // Convert to base64 for a shorter representation (44 chars vs 64 hex chars)
        // Remove padding characters to make it URL-safe
        return Convert.ToBase64String(hashBytes).TrimEnd('=');
    }

    /// <inheritdoc />
    public async Task<(Guid JobId, bool IsNew)> GetOrCreateJobAsync(
        Guid documentId,
        Guid? tenantId,
        byte[] sha256Hash,
        string? extractionProfile = null,
        string? correlationId = null,
        byte priority = 0)
    {
        // Compute idempotency key
        string idempotencyKey = ComputeIdempotencyKey(tenantId, sha256Hash, extractionProfile);

        _logger.LogDebug(
            "Looking for existing job with idempotency key: {IdempotencyKey}",
            idempotencyKey);

        // Check for existing non-terminal job with the same idempotency key
        ProcessJob? existingJob = await _dbContext.ProcessJobs
            .FirstOrDefaultAsync(j =>
                j.IdempotencyKey == idempotencyKey &&
                (j.Status == ProcessJobStatus.Pending || j.Status == ProcessJobStatus.Processing));

        if (existingJob != null)
        {
            _logger.LogInformation(
                "Found existing non-terminal job: JobId={JobId}, Status={Status}, Stage={Stage}",
                existingJob.JobId,
                existingJob.Status,
                existingJob.Stage);

            return (existingJob.JobId, false);
        }

        // No existing job found, create a new one
        ProcessJob newJob = new()
        {
            JobId = Guid.NewGuid(),
            DocumentId = documentId,
            IdempotencyKey = idempotencyKey,
            Status = ProcessJobStatus.Pending,
            Stage = ProcessJobStage.Uploaded,
            Attempts = 0,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            ExtractionProfile = extractionProfile,
            Priority = priority
        };

        _dbContext.ProcessJobs.Add(newJob);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Created new process job: JobId={JobId}, DocumentId={DocumentId}, IdempotencyKey={IdempotencyKey}",
            newJob.JobId,
            newJob.DocumentId,
            newJob.IdempotencyKey);

        return (newJob.JobId, true);
    }
}
