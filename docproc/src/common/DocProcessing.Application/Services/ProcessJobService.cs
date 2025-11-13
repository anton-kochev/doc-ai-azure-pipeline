using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.Application.Services;

/// <summary>
/// Implementation of process job management operations.
/// </summary>
public sealed partial class ProcessJobService : IProcessJobService
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

    /// <inheritdoc />
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
    /// Thrown when the database update operation fails.
    /// </exception>
    public async Task<string> RetryFailedJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        ProcessJob? job = await _dbContext.ProcessJobs
            .FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);

        if (job == null)
        {
            LogJobNotFoundForRetry(jobId);
            throw new JobNotFoundException(jobId);
        }

        if (job.Status != ProcessJobStatus.Failed)
        {
            LogCannotRetryJobNotFailed(job.CorrelationId, job.JobId, job.Status);
            throw new InvalidStateTransitionException(job.JobId, job.Status, ProcessJobStatus.Pending);
        }
        
        job.Status = ProcessJobStatus.Pending;
        job.Stage = ProcessJobStage.Uploaded;
        job.LastErrorCode = null;
        job.LastErrorMessage = null;
        job.StartedAtUtc = null;
        job.CompletedAtUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogJobTransitionedToPendingForRetry(job.CorrelationId, job.JobId, job.Attempts);

        return job.CorrelationId;
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
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
    /// Thrown when the database update operation fails.
    /// </exception>
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

        LogLookingForExistingJob(idempotencyKey);

        // Check for existing non-terminal job with the same idempotency key
        ProcessJob? existingJob = await _dbContext.ProcessJobs
            .FirstOrDefaultAsync(x =>
                x.IdempotencyKey == idempotencyKey &&
                (x.Status == ProcessJobStatus.Pending || x.Status == ProcessJobStatus.Processing));

        if (existingJob != null)
        {
            LogFoundExistingNonTerminalJob(existingJob.CorrelationId, existingJob.JobId, existingJob.Status, existingJob.Stage);

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

        LogCreatedNewProcessJob(newJob.CorrelationId, newJob.JobId, newJob.DocumentId, newJob.IdempotencyKey);

        return (newJob.JobId, true);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the job is not in Pending status or after maximum retry attempts due to concurrency conflicts.
    /// </exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
    /// Thrown when the database update operation fails.
    /// </exception>
    public async Task StartProcessingAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        ProcessJob updatedJob = await TryUpdateJobAsync(
            jobId,
            job =>
            {
                if (job.Status != ProcessJobStatus.Pending)
                {
                    LogCannotStartProcessingNotPending(job.CorrelationId, jobId, job.Status);
                    throw new InvalidStateTransitionException(job.JobId, job.Status, ProcessJobStatus.Processing);
                }
                job.Status = ProcessJobStatus.Processing;
                job.StartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                job.Attempts++;
            },
            "StartProcessing",
            cancellationToken);

        LogJobTransitionedToProcessing(updatedJob.CorrelationId, updatedJob.JobId, updatedJob.Attempts);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the job is not in Processing status or after maximum retry attempts due to concurrency conflicts.
    /// </exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
    /// Thrown when the database update operation fails.
    /// </exception>
    public async Task CompleteJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        ProcessJob updatedJob = await TryUpdateJobAsync(
            jobId,
            job =>
            {
                if (job.Status != ProcessJobStatus.Processing)
                {
                    LogCannotCompleteNotProcessing(job.CorrelationId, jobId, job.Status);
                    throw new InvalidStateTransitionException(job.JobId, job.Status, ProcessJobStatus.Completed);
                }
                job.Status = ProcessJobStatus.Completed;
                job.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            },
            "Complete",
            cancellationToken);

        LogJobCompletedSuccessfully(updatedJob.CorrelationId, updatedJob.JobId, updatedJob.CompletedAtUtc - updatedJob.StartedAtUtc);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the job is not in Processing status or after maximum retry attempts due to concurrency conflicts.
    /// </exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
    /// Thrown when the database update operation fails.
    /// </exception>
    public async Task FailJobAsync(
        Guid jobId,
        string? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        ProcessJob updatedJob = await TryUpdateJobAsync(
            jobId,
            job =>
            {
                if (job.Status != ProcessJobStatus.Processing)
                {
                    LogCannotFailNotProcessing(job.CorrelationId, jobId, job.Status);
                    throw new InvalidStateTransitionException(job.JobId, job.Status, ProcessJobStatus.Failed);
                }
                job.Status = ProcessJobStatus.Failed;
                job.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                job.LastErrorCode = errorCode;
                job.LastErrorMessage = errorMessage;
            },
            "Fail",
            cancellationToken);

        LogJobFailed(updatedJob.CorrelationId, jobId, errorCode, errorMessage);
    }

    private async Task<ProcessJob> TryUpdateJobAsync(
        Guid jobId,
        Action<ProcessJob> updateAction,
        string actionName,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            // Check for cancellation before making database call
            cancellationToken.ThrowIfCancellationRequested();

            ProcessJob? job = await _dbContext.ProcessJobs.FindAsync([jobId], cancellationToken);

            if (job == null)
            {
                LogCannotUpdateJobNotFound(actionName, jobId);
                throw new JobNotFoundException(jobId);
            }

            try
            {
                updateAction(job);
                await _dbContext.SaveChangesAsync(cancellationToken);
                // Detach the entity after successful save to prevent tracking conflicts in subsequent operations
                _dbContext.Entry(job).State = EntityState.Detached;

                return job;
            }
            catch (InvalidStateTransitionException)
            {
                // Rethrow domain exceptions immediately - these are not retryable
                throw;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                LogConcurrencyConflictWhenUpdatingJob(ex, actionName, job.JobId, attempt + 1);

                // Detach the conflicted entity to allow retry
                _dbContext.Entry(job).State = EntityState.Detached;

                if (attempt == maxRetries - 1)
                {
                    throw new InvalidOperationException(
                        $"Failed to {actionName} job {jobId} after {maxRetries} attempts due to concurrency conflicts", ex);
                }

                // Exponential backoff with proper cancellation support
                try
                {
                    int delayMs = (int)Math.Pow(2, attempt) * 100;
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    LogOperationCancelledDuringRetry(actionName, jobId, attempt + 1);
                    throw;
                }
            }
        }

        // This line should never be reached, but is needed for compilation
        throw new InvalidOperationException($"Failed to {actionName} job {jobId} after unexpected error");
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Cannot retry job: Job not found. JobId={JobId}")]
    private partial void LogJobNotFoundForRetry(Guid jobId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Cannot retry job: Job is not in Failed state. CorrelationId: {CorrelationId}, JobId={JobId}, Status={Status}")]
    private partial void LogCannotRetryJobNotFailed(string correlationId, Guid jobId, ProcessJobStatus status);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Job transitioned to Pending for retry. CorrelationId: {CorrelationId}, JobId={JobId}, TotalAttempts={Attempts}")]
    private partial void LogJobTransitionedToPendingForRetry(string correlationId, Guid jobId, int attempts);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Looking for existing job with idempotency key: {IdempotencyKey}")]
    private partial void LogLookingForExistingJob(string idempotencyKey);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Found existing non-terminal job. CorrelationId: {CorrelationId}, JobId={JobId}, Status={Status}, Stage={Stage}")]
    private partial void LogFoundExistingNonTerminalJob(string correlationId, Guid jobId, ProcessJobStatus status, ProcessJobStage stage);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Created new process job. CorrelationId: {CorrelationId}, JobId={JobId}, DocumentId={DocumentId}, IdempotencyKey={IdempotencyKey}")]
    private partial void LogCreatedNewProcessJob(string correlationId, Guid jobId, Guid documentId, string idempotencyKey);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "Cannot start processing: Job is not in Pending status. CorrelationId: {CorrelationId}, JobId={JobId}, CurrentStatus={Status}")]
    private partial void LogCannotStartProcessingNotPending(string correlationId, Guid jobId, ProcessJobStatus status);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Job transitioned to Processing. CorrelationId: {CorrelationId}, JobId={JobId}, Attempts={Attempts}")]
    private partial void LogJobTransitionedToProcessing(string correlationId, Guid jobId, int attempts);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "Cannot complete: Job is not in Processing status. CorrelationId: {CorrelationId}, JobId={JobId}, CurrentStatus={Status}")]
    private partial void LogCannotCompleteNotProcessing(string correlationId, Guid jobId, ProcessJobStatus status);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Job completed successfully. CorrelationId: {CorrelationId}, JobId={JobId}, Duration={Duration}")]
    private partial void LogJobCompletedSuccessfully(string correlationId, Guid jobId, TimeSpan? duration);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Warning,
        Message = "Cannot fail: Job is not in Processing status. CorrelationId: {CorrelationId}, JobId={JobId}, CurrentStatus={Status}")]
    private partial void LogCannotFailNotProcessing(string correlationId, Guid jobId, ProcessJobStatus status);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "Job failed. CorrelationId: {CorrelationId}, JobId={JobId}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}")]
    private partial void LogJobFailed(string correlationId, Guid jobId, string? errorCode, string? errorMessage);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Warning,
        Message = "{Action}: Cannot update job. Job not found. JobId={JobId}")]
    private partial void LogCannotUpdateJobNotFound(string action, Guid jobId);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Warning,
        Message = "{Action}: Invalid operation when updating job. JobId={JobId}, Message={Message}")]
    private partial void LogInvalidOperationWhenUpdatingJob(Exception exception, string action, Guid jobId, string message);

    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Warning,
        Message = "{Action}: Concurrency conflict when updating job. JobId={JobId}, Attempt={Attempt}")]
    private partial void LogConcurrencyConflictWhenUpdatingJob(Exception exception, string action, Guid jobId, int attempt);

    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Information,
        Message = "{Action}: Operation cancelled during retry. JobId={JobId}, Attempt={Attempt}")]
    private partial void LogOperationCancelledDuringRetry(string action, Guid jobId, int attempt);
}
