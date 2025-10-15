using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.Application.Services;

/// <summary>
/// Implementation of process job management operations.
/// </summary>
public sealed class ProcessJobService : IProcessJobService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<ProcessJobService> _logger;
    private readonly TimeProvider _timeProvider;

    public ProcessJobService(
        IApplicationDbContext dbContext,
        ILogger<ProcessJobService> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<bool> RetryFailedJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        ProcessJob? job = await _dbContext.ProcessJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogError("Cannot retry job: Job not found. JobId={JobId}", jobId);
            return false;
        }

        if (job.Status != ProcessJobStatus.Failed)
        {
            _logger.LogWarning("Cannot retry job: Job is not in Failed state. JobId={JobId}, Status={Status}",
                job.JobId, job.Status);
            return false;
        }

        _dbContext.ProcessJobs.Attach(job);
        job.Status = ProcessJobStatus.Pending;
        job.Stage = ProcessJobStage.Uploaded;
        job.LastErrorCode = null;
        job.LastErrorMessage = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job transitioned to Pending for retry. JobId={JobId}, TotalAttempts={Attempts}",
            job.JobId, job.Attempts);

        return true;
    }

    /// <inheritdoc />
    public string ComputeIdempotencyKey(
        Guid? tenantId,
        byte[] sha256Hash,
        string? extractionProfile)
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
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));

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
        byte priority = 0,
        CancellationToken cancellationToken = default)
    {
        // Compute idempotency key
        string idempotencyKey = ComputeIdempotencyKey(tenantId, sha256Hash, extractionProfile);

        _logger.LogDebug(
            "Looking for existing job with idempotency key: {IdempotencyKey}",
            idempotencyKey);

        // Check for existing non-terminal job with the same idempotency key
        ProcessJob? existingJob = await _dbContext.ProcessJobs
            .FirstOrDefaultAsync(x =>
                x.IdempotencyKey == idempotencyKey &&
                (x.Status == ProcessJobStatus.Pending || x.Status == ProcessJobStatus.Processing));

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
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created new process job: JobId={JobId}, DocumentId={DocumentId}, IdempotencyKey={IdempotencyKey}",
            newJob.JobId,
            newJob.DocumentId,
            newJob.IdempotencyKey);

        return (newJob.JobId, true);
    }

    /// <inheritdoc />
    public async Task<bool> StartProcessingAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        (bool success, ProcessJob? updatedJob) = await TryUpdateJobAsync(
            jobId,
            job =>
            {
                if (job.Status != ProcessJobStatus.Pending)
                {
                    _logger.LogWarning(
                        "Cannot start processing: Job is not in Pending status. JobId={JobId}, CurrentStatus={Status}",
                        jobId,
                        job.Status);
                    throw new InvalidOperationException($"Job is not in Pending status (current={job.Status})");
                }
                job.Status = ProcessJobStatus.Processing;
                job.StartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                job.Attempts++;
            },
            "StartProcessing",
            cancellationToken);

        if (success)
        {
            _logger.LogInformation(
                "Job transitioned to Processing. JobId={JobId}, Attempts={Attempts}",
                updatedJob!.JobId,
                updatedJob.Attempts);
        }
        
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> CompleteJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        (bool success, ProcessJob? updatedJob) = await TryUpdateJobAsync(
            jobId,
            job =>
            {
                if (job.Status != ProcessJobStatus.Processing)
                {
                    _logger.LogWarning(
                        "Cannot complete: Job is not in Processing status. JobId={JobId}, CurrentStatus={Status}",
                        jobId,
                        job.Status);
                    throw new InvalidOperationException($"Job is not in Processing status (current={job.Status})");
                }
                job.Status = ProcessJobStatus.Completed;
                job.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            },
            "Complete",
            cancellationToken);
        
        if (success)
        {
            _logger.LogInformation(
                "Job completed successfully. JobId={JobId}, Duration={Duration}",
                updatedJob!.JobId,
                updatedJob.CompletedAtUtc - updatedJob.StartedAtUtc);
        }
        
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> FailJobAsync(
        Guid jobId,
        string? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        (bool success, _) = await TryUpdateJobAsync(
            jobId,
            job =>
            {
                if (job.Status != ProcessJobStatus.Processing)
                {
                    _logger.LogWarning(
                        "Cannot fail: Job is not in Processing status. JobId={JobId}, CurrentStatus={Status}",
                        jobId,
                        job.Status);
                    throw new InvalidOperationException($"Job is not in Processing status (current={job.Status})");
                }
                job.Status = ProcessJobStatus.Failed;
                job.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                job.LastErrorCode = errorCode;
                job.LastErrorMessage = errorMessage;
            },
            "Fail",
            cancellationToken);
        
        if (success)
        {
            _logger.LogError(
                "Job failed. JobId={JobId}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                jobId,
                errorCode,
                errorMessage);
        }
        
        return success;
    }

    private async Task<(bool Success, ProcessJob? Job)> TryUpdateJobAsync(
        Guid jobId,
        Action<ProcessJob> updateAction,
        string actionName,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            ProcessJob? job = await _dbContext.ProcessJobs.FindAsync([jobId], cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("{Action}: Cannot update job. Job not found. JobId={JobId}", actionName, jobId);
                return (false, null);
            }

            try
            {
                updateAction(job);
                await _dbContext.SaveChangesAsync(cancellationToken);
                // Detach the entity after successful save to prevent tracking conflicts in subsequent operations
                _dbContext.Entry(job).State = EntityState.Detached;

                return (Success: true, Job: job);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Action}: Invalid operation when updating job. JobId={JobId}, Message={Message}",
                    actionName,
                    job.JobId,
                    ex.Message);
                
                return (false, Job: job);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Action}: Concurrency conflict when updating job. JobId={JobId}, Attempt={Attempt}",
                    actionName,
                    job.JobId,
                    attempt + 1);

                // Detach the conflicted entity to allow retry
                _dbContext.Entry(job).State = EntityState.Detached;

                if (attempt == maxRetries - 1)
                {
                    throw new InvalidOperationException(
                        $"Failed to start job {jobId} after {maxRetries} attempts due to concurrency conflicts", ex);
                }

                // Exponential backoff before retry
                await Task.Delay(
                    TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
                    cancellationToken);
            }
        }

        return (Success: false, Job: null);
    }

}
