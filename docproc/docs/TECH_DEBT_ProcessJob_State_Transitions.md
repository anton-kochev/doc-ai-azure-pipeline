# Technical Debt: ProcessJob State Transitions

**Created**: 2025-10-14
**Status**: Open
**Related Files**:
- `src/DocProcessing.Api/Services/IProcessJobService.cs`
- `src/DocProcessing.Api/Services/ProcessJobService.cs`
- `src/common/DocProcessing.Domain/Entities/ProcessJob.cs`
- `src/DocProcessing.Api.Tests/Services/ProcessJobServiceTests.cs`

---

## Executive Summary

Code review identified critical concurrency issues and design gaps in the ProcessJob state transition implementation. While the basic functionality works and has good test coverage, production deployment requires addressing race conditions and improving error handling.

---

## Critical Issues (Must Fix Before Production)

### 1. Concurrency Race Conditions ⚠️

**Severity**: Critical
**Impact**: Data corruption, duplicate processing, incorrect state transitions

#### Problem

The `ProcessJob` entity has a `RowVersion` property with `[Timestamp]` attribute but we're not using it for optimistic concurrency control. This creates serious race conditions in distributed scenarios.

**Scenario**: Two workers simultaneously try to process the same job:
1. Worker A reads job (Status = Pending)
2. Worker B reads job (Status = Pending)
3. Worker A calls `StartProcessingAsync` → Status becomes Processing
4. Worker B calls `StartProcessingAsync` → Status becomes Processing AGAIN
5. Both workers process the same document simultaneously

#### Solution

Implement optimistic concurrency with retry logic using Entity Framework's `DbUpdateConcurrencyException`:

```csharp
public async Task<bool> StartProcessingAsync(Guid jobId, CancellationToken cancellationToken = default)
{
    const int maxRetries = 3;

    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(
            new object[] { jobId },
            cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Cannot start processing: Job not found. JobId={JobId}", jobId);
            return false;
        }

        if (job.Status != ProcessJobStatus.Pending)
        {
            _logger.LogWarning(
                "Cannot start processing: Job is not in Pending status. JobId={JobId}, CurrentStatus={Status}",
                jobId,
                job.Status);
            return false;
        }

        job.Status = ProcessJobStatus.Processing;
        job.StartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        job.Attempts++;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Job transitioned to Processing. JobId={JobId}, Attempts={Attempts}",
                jobId,
                job.Attempts);

            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Concurrency conflict when starting job. JobId={JobId}, Attempt={Attempt}",
                jobId,
                attempt + 1);

            // Detach the conflicted entity to allow retry
            _dbContext.Entry(job).State = EntityState.Detached;

            if (attempt == maxRetries - 1)
            {
                throw new InvalidOperationException(
                    $"Failed to start job {jobId} after {maxRetries} attempts due to concurrency conflicts",
                    ex);
            }

            // Exponential backoff before retry
            await Task.Delay(
                TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
                cancellationToken);
        }
    }

    return false;
}
```

**Files to Modify**:
- `ProcessJobService.cs`: Apply retry pattern to `StartProcessingAsync`, `CompleteJobAsync`, `FailJobAsync`

**Estimated Effort**: 4-6 hours (including testing)

---

### 2. Error Handling - Boolean Returns Are Problematic ⚠️

**Severity**: Critical
**Impact**: Silent failures, difficult debugging, unclear error conditions

#### Problem

Current approach returns `false` for multiple failure conditions:
- Job not found
- Invalid state transition
- Concurrency conflict

Callers cannot distinguish between these scenarios without additional database queries.

#### Solution

Use domain-specific exceptions for different failure modes:

**Create Exception Types**:

```csharp
// File: src/common/DocProcessing.Domain/Exceptions/JobNotFoundException.cs
namespace DocProcessing.Domain.Exceptions;

public sealed class JobNotFoundException : Exception
{
    public Guid JobId { get; }

    public JobNotFoundException(Guid jobId)
        : base($"Job {jobId} not found")
    {
        JobId = jobId;
    }
}
```

```csharp
// File: src/common/DocProcessing.Domain/Exceptions/InvalidStateTransitionException.cs
namespace DocProcessing.Domain.Exceptions;

public sealed class InvalidStateTransitionException : Exception
{
    public Guid JobId { get; }
    public ProcessJobStatus CurrentStatus { get; }
    public ProcessJobStatus AttemptedStatus { get; }

    public InvalidStateTransitionException(
        Guid jobId,
        ProcessJobStatus currentStatus,
        ProcessJobStatus attemptedStatus)
        : base($"Cannot transition job {jobId} from {currentStatus} to {attemptedStatus}")
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}
```

**Update Interface**:

```csharp
/// <summary>
/// Transitions a job from Pending to Processing status.
/// </summary>
/// <param name="jobId">The ID of the job to start processing.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <exception cref="JobNotFoundException">Thrown when the job does not exist.</exception>
/// <exception cref="InvalidStateTransitionException">Thrown when the job is not in Pending status.</exception>
/// <exception cref="InvalidOperationException">Thrown when unable to acquire the job due to concurrency conflicts after retries.</exception>
Task StartProcessingAsync(Guid jobId, CancellationToken cancellationToken = default);
```

**Update Implementation**:

```csharp
if (job == null)
{
    _logger.LogWarning("Cannot start processing: Job not found. JobId={JobId}", jobId);
    throw new JobNotFoundException(jobId);
}

if (job.Status != ProcessJobStatus.Pending)
{
    _logger.LogWarning(
        "Cannot start processing: Job is not in Pending status. JobId={JobId}, CurrentStatus={Status}",
        jobId,
        job.Status);
    throw new InvalidStateTransitionException(jobId, job.Status, ProcessJobStatus.Processing);
}
```

**Files to Create**:
- `src/common/DocProcessing.Domain/Exceptions/JobNotFoundException.cs`
- `src/common/DocProcessing.Domain/Exceptions/InvalidStateTransitionException.cs`

**Files to Modify**:
- `IProcessJobService.cs`: Update method signatures and XML documentation
- `ProcessJobService.cs`: Throw exceptions instead of returning false
- `ProcessJobServiceTests.cs`: Update tests to expect exceptions

**Estimated Effort**: 3-4 hours (including test updates)

---

## High Priority Issues

### 3. Incomplete State Machine - ManualReview Integration

**Severity**: High
**Impact**: Incomplete workflow, missing business functionality

#### Problem

`ManualReview` status exists in the enum but has no transitions to or from it. Real-world scenarios require:
- Processing → ManualReview (when validation fails but isn't an outright error)
- ManualReview → Processing (when reprocessing after manual intervention)
- ManualReview → Completed (when manually marked as complete)
- ManualReview → Failed (when manually rejected)

#### Solution

**Create State Transition Validator**:

```csharp
// File: src/common/DocProcessing.Domain/Entities/ProcessJobStatusTransitions.cs
namespace DocProcessing.Domain.Entities;

public static class ProcessJobStatusTransitions
{
    private static readonly Dictionary<ProcessJobStatus, HashSet<ProcessJobStatus>> ValidTransitions = new()
    {
        [ProcessJobStatus.Pending] = [ProcessJobStatus.Processing],
        [ProcessJobStatus.Processing] = [
            ProcessJobStatus.Completed,
            ProcessJobStatus.Failed,
            ProcessJobStatus.ManualReview
        ],
        [ProcessJobStatus.ManualReview] = [
            ProcessJobStatus.Processing,
            ProcessJobStatus.Completed,
            ProcessJobStatus.Failed
        ],
        [ProcessJobStatus.Completed] = [],
        [ProcessJobStatus.Failed] = [ProcessJobStatus.Pending], // Allow retry
    };

    public static bool IsValidTransition(ProcessJobStatus from, ProcessJobStatus to)
    {
        return ValidTransitions.TryGetValue(from, out HashSet<ProcessJobStatus>? allowed)
            && allowed.Contains(to);
    }

    public static IReadOnlySet<ProcessJobStatus> GetValidTransitions(ProcessJobStatus from)
    {
        return ValidTransitions.TryGetValue(from, out HashSet<ProcessJobStatus>? allowed)
            ? allowed
            : new HashSet<ProcessJobStatus>();
    }
}
```

**Add New Service Methods**:

```csharp
/// <summary>
/// Transitions a job to ManualReview status when automated processing encounters issues.
/// </summary>
/// <param name="jobId">The ID of the job to send to manual review.</param>
/// <param name="reason">The reason for manual review.</param>
/// <param name="cancellationToken">Cancellation token.</param>
Task SendToManualReviewAsync(Guid jobId, string reason, CancellationToken cancellationToken = default);

/// <summary>
/// Transitions a job from ManualReview back to Pending for reprocessing.
/// </summary>
/// <param name="jobId">The ID of the job to retry.</param>
/// <param name="cancellationToken">Cancellation token.</param>
Task RetryFromManualReviewAsync(Guid jobId, CancellationToken cancellationToken = default);

/// <summary>
/// Manually completes a job that is in ManualReview status.
/// </summary>
/// <param name="jobId">The ID of the job to complete.</param>
/// <param name="cancellationToken">Cancellation token.</param>
Task CompleteFromManualReviewAsync(Guid jobId, CancellationToken cancellationToken = default);
```

**Implementation Example**:

```csharp
public async Task SendToManualReviewAsync(
    Guid jobId,
    string reason,
    CancellationToken cancellationToken = default)
{
    const int maxRetries = 3;

    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(
            new object[] { jobId },
            cancellationToken);

        if (job == null)
        {
            throw new JobNotFoundException(jobId);
        }

        if (!ProcessJobStatusTransitions.IsValidTransition(job.Status, ProcessJobStatus.ManualReview))
        {
            throw new InvalidStateTransitionException(
                job.JobId,
                job.Status,
                ProcessJobStatus.ManualReview);
        }

        job.Status = ProcessJobStatus.ManualReview;
        job.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        job.LastErrorMessage = reason;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Job sent to manual review. JobId={JobId}, Reason={Reason}",
                jobId,
                reason);

            return;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Concurrency conflict. JobId={JobId}, Attempt={Attempt}",
                jobId,
                attempt + 1);
            _dbContext.Entry(job).State = EntityState.Detached;

            if (attempt == maxRetries - 1)
            {
                throw new InvalidOperationException(
                    $"Failed to send job {jobId} to manual review after {maxRetries} attempts",
                    ex);
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
                cancellationToken);
        }
    }
}
```

**Files to Create**:
- `src/common/DocProcessing.Domain/Entities/ProcessJobStatusTransitions.cs`

**Files to Modify**:
- `IProcessJobService.cs`: Add new methods
- `ProcessJobService.cs`: Implement new methods with state validation
- `ProcessJobServiceTests.cs`: Add tests for new methods

**Estimated Effort**: 6-8 hours

---

### 4. Missing CancellationToken Support

**Severity**: Medium-High
**Impact**: Cannot cancel long-running operations, poor ASP.NET Core integration

#### Problem

All async methods should accept `CancellationToken` for proper cancellation support. This is especially important in ASP.NET Core scenarios where HTTP requests can be aborted.

#### Solution

Update all async method signatures:

```csharp
Task<(Guid JobId, bool IsNew)> GetOrCreateJobAsync(
    Guid documentId,
    Guid? tenantId,
    byte[] sha256Hash,
    string? extractionProfile = null,
    string? correlationId = null,
    byte priority = 0,
    CancellationToken cancellationToken = default);

Task StartProcessingAsync(Guid jobId, CancellationToken cancellationToken = default);
Task CompleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);
Task FailJobAsync(
    Guid jobId,
    string? errorCode = null,
    string? errorMessage = null,
    CancellationToken cancellationToken = default);
```

Then pass `cancellationToken` to all EF Core calls:
- `FindAsync(..., cancellationToken)`
- `SaveChangesAsync(cancellationToken)`
- `FirstOrDefaultAsync(..., cancellationToken)`
- `Task.Delay(..., cancellationToken)`

**Files to Modify**:
- `IProcessJobService.cs`
- `ProcessJobService.cs`
- `ProcessJobServiceTests.cs`

**Estimated Effort**: 2-3 hours

---

## Medium Priority Issues

### 5. Repository Pattern for Better Separation of Concerns

**Severity**: Medium
**Impact**: Code organization, testability, maintainability

#### Problem

`ProcessJobService` is doing two jobs: business logic (state transitions) AND data access. This violates Single Responsibility Principle.

#### Solution

Introduce repository pattern:

```csharp
// File: src/common/DocProcessing.Application/Interfaces/IProcessJobRepository.cs
namespace DocProcessing.Application.Interfaces;

public interface IProcessJobRepository
{
    Task<ProcessJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<ProcessJob?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ProcessJob> AddAsync(ProcessJob job, CancellationToken cancellationToken = default);

    Task UpdateAsync(ProcessJob job, CancellationToken cancellationToken = default);

    Task<List<ProcessJob>> GetPendingJobsAsync(
        int maxCount,
        CancellationToken cancellationToken = default);
}
```

```csharp
// File: src/DocProcessing.Api/Repositories/ProcessJobRepository.cs
public sealed class ProcessJobRepository : IProcessJobRepository
{
    private readonly AppDbContext _dbContext;

    public ProcessJobRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProcessJob?> GetByIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProcessJobs.FindAsync(
            new object[] { jobId },
            cancellationToken);
    }

    public async Task<ProcessJob?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProcessJobs
            .FirstOrDefaultAsync(
                j => j.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public async Task<ProcessJob> AddAsync(
        ProcessJob job,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ProcessJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateAsync(
        ProcessJob job,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ProcessJob>> GetPendingJobsAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProcessJobs
            .Where(j => j.Status == ProcessJobStatus.Pending)
            .OrderBy(j => j.Priority)
            .ThenBy(j => j.CreatedAtUtc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }
}
```

Update `ProcessJobService` to use the repository instead of direct DbContext access.

**Files to Create**:
- `src/common/DocProcessing.Application/Interfaces/IProcessJobRepository.cs`
- `src/DocProcessing.Api/Repositories/ProcessJobRepository.cs`

**Files to Modify**:
- `ProcessJobService.cs`: Inject and use repository
- `Program.cs`: Register repository in DI
- `ProcessJobServiceTests.cs`: Mock repository instead of DbContext

**Estimated Effort**: 8-10 hours

---

## Low Priority / Nice to Have

### 6. Missing Test Scenarios

Add tests for:

```csharp
[Fact]
public async Task StartProcessingAsync_WhenConcurrentCalls_OnlyOneSucceeds()
{
    // Test that concurrent state transitions are handled correctly
}

[Fact]
public async Task FailJobAsync_PreservesStartedAtUtc()
{
    // Ensure StartedAtUtc isn't lost when job fails
}

[Fact]
public async Task StartProcessingAsync_WhenJobIsManualReview_ThrowsException()
{
    // Test ManualReview status is handled correctly
}

[Fact]
public async Task CompleteJobAsync_WhenStartedAtUtcIsNull_HandlesGracefully()
{
    // Edge case: data migration or manual updates
}

[Theory]
[InlineData(ProcessJobStatus.Pending, ProcessJobStatus.Processing, true)]
[InlineData(ProcessJobStatus.Processing, ProcessJobStatus.Completed, true)]
[InlineData(ProcessJobStatus.Processing, ProcessJobStatus.Failed, true)]
[InlineData(ProcessJobStatus.Pending, ProcessJobStatus.Completed, false)]
[InlineData(ProcessJobStatus.Completed, ProcessJobStatus.Processing, false)]
public async Task StateTransitions_ValidatesCorrectly(
    ProcessJobStatus fromStatus,
    ProcessJobStatus toStatus,
    bool shouldSucceed)
{
    // Comprehensive state transition validation
}
```

**Estimated Effort**: 3-4 hours

---

### 7. Batch Operations for High-Throughput Scenarios

**Impact**: Performance optimization for processing multiple jobs

```csharp
public async Task<int> StartProcessingBatchAsync(
    IEnumerable<Guid> jobIds,
    CancellationToken cancellationToken = default)
{
    List<Guid> jobIdList = jobIds.ToList();

    // Single query to get all jobs
    List<ProcessJob> jobs = await _dbContext.ProcessJobs
        .Where(j => jobIdList.Contains(j.JobId) && j.Status == ProcessJobStatus.Pending)
        .ToListAsync(cancellationToken);

    if (jobs.Count == 0)
    {
        return 0;
    }

    DateTimeOffset now = _timeProvider.GetUtcNow();
    foreach (ProcessJob job in jobs)
    {
        job.Status = ProcessJobStatus.Processing;
        job.StartedAtUtc = now.UtcDateTime;
        job.Attempts++;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    _logger.LogInformation("Batch started processing {Count} jobs", jobs.Count);
    return jobs.Count;
}
```

**Estimated Effort**: 4-5 hours

---

### 8. Structured Logging with LoggerMessage Source Generators

**Impact**: Performance optimization, compile-time checking

```csharp
public sealed partial class ProcessJobService : IProcessJobService
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Job transitioned to Processing. JobId={JobId}, Attempts={Attempts}")]
    private partial void LogJobStartedProcessing(Guid jobId, int attempts);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Cannot start processing: Job not found. JobId={JobId}")]
    private partial void LogJobNotFound(Guid jobId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Cannot start processing: Job is not in Pending status. JobId={JobId}, CurrentStatus={Status}")]
    private partial void LogInvalidStateForProcessing(Guid jobId, ProcessJobStatus status);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Job completed successfully. JobId={JobId}, Duration={Duration}")]
    private partial void LogJobCompleted(Guid jobId, TimeSpan? duration);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Error,
        Message = "Job failed. JobId={JobId}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}")]
    private partial void LogJobFailed(Guid jobId, string? errorCode, string? errorMessage);
}
```

**Estimated Effort**: 2-3 hours

---

### 9. Duration Calculation Safety

**Impact**: Defensive coding, prevents null reference exceptions

```csharp
// In CompleteJobAsync
TimeSpan? duration = job.StartedAtUtc.HasValue
    ? job.CompletedAtUtc - job.StartedAtUtc.Value
    : null;

_logger.LogInformation(
    "Job completed successfully. JobId={JobId}, Duration={Duration}",
    jobId,
    duration?.ToString() ?? "Unknown");
```

**Estimated Effort**: 1 hour

---

## Implementation Priority

### Phase 1: Critical Fixes (Before Production)
1. Concurrency control with retry logic (4-6h)
2. Exception-based error handling (3-4h)
3. CancellationToken support (2-3h)

**Total: 9-13 hours**

### Phase 2: Complete State Machine (Next Sprint)
4. ManualReview state transitions (6-8h)
5. State transition validator (included in #4)

**Total: 6-8 hours**

### Phase 3: Architecture Improvements (Future)
6. Repository pattern (8-10h)
7. Missing test scenarios (3-4h)

**Total: 11-14 hours**

### Phase 4: Optimizations (When Needed)
8. Batch operations (4-5h)
9. Structured logging (2-3h)
10. Duration calculation safety (1h)

**Total: 7-9 hours**

---

## Testing Checklist

After implementing fixes:

- [ ] All existing tests pass
- [ ] New concurrency tests pass
- [ ] Exception handling tests added
- [ ] ManualReview transition tests added
- [ ] Load test with multiple workers
- [ ] Verify RowVersion is being updated
- [ ] Check database index usage
- [ ] Performance benchmark before/after

---

## References

- [EF Core Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [LoggerMessage Source Generators](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
- [Repository Pattern in Clean Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [CancellationToken Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)

---

## Notes

- This technical debt was identified during code review on 2025-10-14
- Current implementation has 50 passing tests but lacks concurrency testing
- Production deployment should wait for Phase 1 completion
- Consider creating GitHub issues for each phase item for tracking
