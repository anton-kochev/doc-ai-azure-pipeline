# Technical Debt: ProcessJob State Transitions

**Created**: 2025-10-14
**Last Updated**: 2025-10-30
**Status**: Partially Resolved
**Resolved**: Concurrency race conditions, CancellationToken support, Structured logging ✅
**Pending**: Error handling, ManualReview state transitions, architecture improvements
**Related Files**:
- `src/common/DocProcessing.Application/Interfaces/IProcessJobService.cs`
- `src/common/DocProcessing.Application/Services/ProcessJobService.cs`
- `src/common/DocProcessing.Domain/Entities/ProcessJob.cs`
- `docproc/tests/Infrastructure.Tests/Services/ProcessJobServiceTests.cs`

---

## Executive Summary

Code review identified design gaps in the ProcessJob state transition implementation. Critical concurrency issues have been resolved (2025-10-15), but production deployment would benefit from improved error handling and complete ManualReview state machine implementation.

**Recently Resolved**:
- ✅ Optimistic concurrency control with retry logic (2025-10-15, Commit `b22a492`)
- ✅ CancellationToken support throughout async operations (2025-10-15, Commit `b22a492`)
- ✅ Entity Framework RowVersion properly utilized (2025-10-15, Commit `b22a492`)
- ✅ Structured logging with LoggerMessage source generators (Already implemented)

---

## Critical Issues

### 1. Error Handling - Boolean Returns Are Problematic ⚠️

**Severity**: High
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

### 2. Incomplete State Machine - ManualReview Integration

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

## Medium Priority Issues

### 3. Repository Pattern for Better Separation of Concerns

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

### 4. Missing Test Scenarios

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

### 5. Batch Operations for High-Throughput Scenarios

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

### 6. Structured Logging with LoggerMessage Source Generators ✅

**Status**: COMPLETED
**Impact**: Performance optimization, compile-time checking

The `ProcessJobService` class already implements structured logging using LoggerMessage source generators (EventId 1-15). All logging operations use compile-time generated methods for optimal performance.

See `ProcessJobService.cs` lines 308-396 for the implementation.

---

### 7. Duration Calculation Safety

**Severity**: Low
**Impact**: Defensive coding, prevents null reference exceptions in edge cases

**Current Issue**: Line 207 in `ProcessJobService.cs` performs duration calculation without null check:
```csharp
LogJobCompletedSuccessfully(updatedJob!.CorrelationId, updatedJob.JobId, updatedJob.CompletedAtUtc - updatedJob.StartedAtUtc);
```

This could throw `InvalidOperationException` if `StartedAtUtc` is null (e.g., data migration scenarios or manual database updates).

**Recommended Fix**:
```csharp
// In CompleteJobAsync
TimeSpan? duration = updatedJob.StartedAtUtc.HasValue
    ? updatedJob.CompletedAtUtc - updatedJob.StartedAtUtc.Value
    : null;

LogJobCompletedSuccessfully(updatedJob.CorrelationId, updatedJob.JobId, duration);
```

Update `LogJobCompletedSuccessfully` signature to accept `TimeSpan?` and format appropriately.

**Estimated Effort**: 1 hour

---

## Implementation Priority

### Phase 1: Remaining Critical Fixes
1. Exception-based error handling (3-4h)

**Total: 3-4 hours**

### Phase 2: Complete State Machine (Next Sprint)
2. ManualReview state transitions (6-8h)
   - Includes state transition validator

**Total: 6-8 hours**

### Phase 3: Architecture Improvements (Future)
3. Repository pattern (8-10h)
4. Missing test scenarios (3-4h)

**Total: 11-14 hours**

### Phase 4: Optimizations (When Needed)
5. Batch operations (4-5h)
6. ~~Structured logging~~ ✅ COMPLETED
7. Duration calculation safety (1h)

**Total: 5-6 hours**

---

## Testing Checklist

Remaining testing tasks:

- [ ] Exception handling tests (for Issue #1)
- [ ] ManualReview transition tests (for Issue #2)
- [ ] Load test with multiple workers
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
- **Updated 2025-10-15**: Critical concurrency issues resolved (commit `b22a492`)
- **Updated 2025-10-30**: Document paths corrected, structured logging marked as completed
- Current implementation has 97+ passing tests with proper concurrency handling
- Production deployment is now safer but would benefit from exception-based error handling (Phase 1 remaining)
- Structured logging with LoggerMessage source generators is already implemented in the codebase
- Consider creating GitHub issues for each phase item for tracking

## Deferred Findings from Chunking Pipeline Review (2026-03-05)

### Medium Priority (deferred)

- **Implicit separator coupling**: `DocumentChunker.cs:111` uses `searchFrom += 1` assuming `"\n"` from `PreprocessStageActivity`. Extract to shared constant (e.g. `BlockSeparator = "\n"`) to prevent future drift. Files: `DocumentChunker.cs`, `PreprocessStageActivity.cs`
- **Output vs Metadata inconsistency**: OCR stage uses `Output` dict while other stages use `Metadata`. Now that `Output` is typed `Dictionary<string, object>?`, consider standardizing all stage output through `Metadata` and reserving `Output` for non-forwarded data
- **TenantId fallback to "default"**: `ChunkStageActivity.cs:91-101` silently falls back to `"default"` tenant. In a multi-tenant system, consider failing instead of silently misrouting data

### Low Priority (deferred)

- **Test region organization**: `ChunkStageActivityTests.cs` — `ExecuteAsync_UsesPreprocessOptionsContainer_WhenDownloading` test sits outside any `#region`
- **Missing multi-page + multi-block offset test**: `DocumentChunkerTests.cs` — no test covers block offsets across page boundary where `"\n\n"` page separator interacts with `"\n"` block separator
- **No constructor null-guard tests**: Pre-existing gap across all stage activity tests — constructor `ArgumentNullException.ThrowIfNull` guards are not tested
- **CreateStageContext missing TenantId**: Most chunk stage tests hit the `"default"` fallback path. Add at least one test with real `TenantId` that verifies the upload path

### Nit (deferred)

- **SentenceBoundaryRegex limitation**: `DocumentChunker.cs:16` — regex `(?<=[.!?])\s+(?=[A-Z\n])` won't split on sentences starting with numbers or non-Latin scripts. Pre-existing, not introduced by chunking changes
- **Offset assertion granularity**: New offset tests verify block count but not exact numeric offset values. More precise assertions would make regressions easier to diagnose

---

## Observations from Code Review (2025-10-30)

### Already Implemented
- ✅ **Structured Logging**: ProcessJobService uses LoggerMessage source generators (EventId 1-15) for all logging operations
- ✅ **Correlation ID**: Included in all log messages for distributed tracing
- ✅ **Concurrency Control**: Retry logic with exponential backoff is properly implemented
- ✅ **Entity State Management**: Proper detachment after concurrency conflicts

### Current Test Coverage
- 97+ unit tests covering:
  - Idempotency key computation (8 tests)
  - GetOrCreateJob scenarios (13 tests)
  - StartProcessing transitions (10 tests)
  - CompleteJob transitions (6 tests)
  - FailJob transitions (9 tests)
- All tests use FakeTimeProvider for deterministic time-based testing
- Tests properly verify logging calls using FakeLogger

### Remaining Priority Work
1. **Critical**: Exception-based error handling to replace boolean returns
2. **High**: ManualReview state machine implementation with proper transitions
3. **Medium**: Repository pattern for separation of concerns
4. **Low**: Duration calculation null safety (edge case)
