# Error Handling Standards

**MANDATORY**: All new code MUST follow the domain-specific exception pattern for error handling. This project uses exceptions (not boolean returns or Result types) for invalid operations and error conditions.

## Exception Handling Philosophy

1. **Use exceptions for exceptional conditions**: Invalid state transitions, resources not found, infrastructure failures
2. **Use domain-specific exceptions**: Create strongly-typed exceptions in the Domain layer, not generic exceptions
3. **Use return values for expected outcomes**: Success/failure with business meaning (e.g., `StageResult` for pipeline stages)
4. **Fail fast**: Let exceptions propagate; don't catch and convert to boolean/null unless there's a specific reason

## Domain Exception Pattern

**Location**: All custom exceptions belong in `src/common/DocProcessing.Domain/Exceptions/`

**Naming Convention**: `{Condition}Exception.cs` (e.g., `JobNotFoundException`, `InvalidStateTransitionException`)

**Exception Structure**:

```csharp
namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Thrown when a job with the specified ID cannot be found.
/// </summary>
public sealed class JobNotFoundException : Exception
{
    public Guid JobId { get; }

    public JobNotFoundException(Guid jobId)
        : base($"Job with ID {jobId} was not found")
    {
        JobId = jobId;
    }
}
```

**Key Requirements**:
- Sealed classes (prevent inheritance)
- Inherit from `Exception` (or a more specific base)
- Include contextual properties (JobId, CurrentStatus, etc.)
- Provide clear, actionable error messages
- Use XML documentation to describe when it's thrown

## When to Use Exceptions

**DO use exceptions for:**

- **Resource not found**: `JobNotFoundException`, `DocumentNotFoundException`
- **Invalid operations**: `InvalidStateTransitionException` for state machine violations
- **Configuration errors**: Missing required settings, invalid configuration values
- **Infrastructure failures**: Database connection failures, Service Bus errors (after retries)
- **Security violations**: Unauthorized access, authentication failures
- **Concurrency conflicts**: After retry attempts are exhausted

**DO NOT use exceptions for:**

- **Expected domain events**: Pipeline stage failures with business meaning (use `StageResult`)
- **Validation results**: User input validation (return validation results)
- **Optional values**: Use nullable types or Option pattern
- **Control flow**: Don't use exceptions for normal branching logic

## Exception Examples

### State Machine Violations

```csharp
/// <summary>
/// Thrown when attempting an invalid state transition.
/// </summary>
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

### Service Implementation

```csharp
public async Task StartProcessingAsync(Guid jobId, CancellationToken cancellationToken = default)
{
    ProcessJob? job = await _dbContext.ProcessJobs.FindAsync([jobId], cancellationToken);

    if (job == null)
    {
        _logger.LogWarning("Cannot start processing: Job not found. JobId={JobId}", jobId);
        throw new JobNotFoundException(jobId);
    }

    if (job.Status != ProcessJobStatus.Pending)
    {
        _logger.LogWarning(
            "Cannot start processing: Invalid state. JobId={JobId}, CurrentStatus={Status}",
            jobId,
            job.Status);
        throw new InvalidStateTransitionException(job.JobId, job.Status, ProcessJobStatus.Processing);
    }

    // Update job state...
}
```

## Integration with Azure Durable Functions

Azure Durable Functions are designed around exception-based error handling:

**Orchestrator Pattern**:

```csharp
public async Task<string> RunOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    Guid jobId = context.GetInput<Guid>();

    try
    {
        // Activity functions throw exceptions on failure
        await context.CallActivityAsync(nameof(StartJob), jobId);
        await context.CallActivityAsync(nameof(ProcessDocument), jobId);
        await context.CallActivityAsync(nameof(CompleteJob), jobId);

        return "Success";
    }
    catch (JobNotFoundException ex)
    {
        _logger.LogError(ex, "Job not found: {JobId}", ex.JobId);
        await context.CallActivityAsync(nameof(FailJob),
            new FailJobRequest(jobId, "JOB_NOT_FOUND", ex.Message));
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Orchestration failed: {JobId}", jobId);
        await context.CallActivityAsync(nameof(FailJob),
            new FailJobRequest(jobId, "ORCHESTRATION_ERROR", ex.Message));
        throw;
    }
}
```

**Activity Function Pattern**:

```csharp
[Function(nameof(StartJob))]
public async Task StartJob(
    [ActivityTrigger] Guid jobId,
    CancellationToken cancellationToken)
{
    // Let exceptions propagate to orchestrator
    await _jobService.StartProcessingAsync(jobId, cancellationToken);

    _logger.LogInformation("Job started successfully. JobId={JobId}", jobId);
}
```

**Key Points**:
- Activity functions should let exceptions propagate
- Orchestrator catches exceptions and handles workflow logic
- Durable Functions automatically retries on transient failures
- Exceptions are tracked in orchestration history

## Testing Exceptions

**Test Structure** (TUnit):

```csharp
[Test]
public async Task StartProcessingAsync_WhenJobNotFound_ThrowsJobNotFoundException()
{
    // Arrange
    Guid nonExistentJobId = Guid.NewGuid();

    // Act & Assert
    var exception = await Assert.That(
        () => _service.StartProcessingAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

    await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
    await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
}

[Test]
public async Task StartProcessingAsync_WhenInvalidState_ThrowsInvalidStateTransitionException()
{
    // Arrange
    Guid jobId = Guid.NewGuid();
    ProcessJob job = new() { JobId = jobId, Status = ProcessJobStatus.Completed };
    await _dbContext.ProcessJobs.AddAsync(job);
    await _dbContext.SaveChangesAsync();

    // Act & Assert
    var exception = await Assert.That(
        () => _service.StartProcessingAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

    await Assert.That(exception!.JobId).IsEqualTo(jobId);
    await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Completed);
    await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Processing);
}
```

**Testing Guidelines**:
- Use `await Assert.That(() => ...).ThrowsExactly<TException>()` for exception assertions
- Verify exception properties contain correct context
- Check exception messages for clarity
- Test logging of exceptions (use FakeLogger)
- Ensure exceptions include correlation IDs in logs

## API Error Responses

**Mapping Exceptions to HTTP Status Codes**:

```csharp
[Function("RetryJob")]
public async Task<IActionResult> RetryJob(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs/{jobId}/retry")]
    HttpRequest req,
    string jobId)
{
    if (!Guid.TryParse(jobId, out Guid parsedJobId))
    {
        return new BadRequestObjectResult(new { error = "Invalid job ID format" });
    }

    try
    {
        string correlationId = await _jobService.RetryFailedJobAsync(
            parsedJobId,
            req.HttpContext.RequestAborted);

        return new OkObjectResult(new
        {
            jobId = parsedJobId,
            correlationId,
            message = "Job retry initiated successfully"
        });
    }
    catch (JobNotFoundException ex)
    {
        _logger.LogWarning(ex, "Job not found for retry. JobId={JobId}", ex.JobId);
        return new NotFoundObjectResult(new
        {
            error = "Job not found",
            jobId = ex.JobId
        });
    }
    catch (InvalidStateTransitionException ex)
    {
        _logger.LogWarning(
            ex,
            "Invalid state for retry. JobId={JobId}, CurrentStatus={CurrentStatus}",
            ex.JobId,
            ex.CurrentStatus);
        return new ConflictObjectResult(new
        {
            error = "Job is not in a retryable state",
            jobId = ex.JobId,
            currentStatus = ex.CurrentStatus.ToString()
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during job retry. JobId={JobId}", parsedJobId);
        return new ObjectResult(new { error = "Internal server error" })
        {
            StatusCode = 500
        };
    }
}
```

**HTTP Status Code Mapping**:
- `JobNotFoundException` → 404 Not Found
- `InvalidStateTransitionException` → 409 Conflict
- `ValidationException` → 400 Bad Request
- `UnauthorizedException` → 401 Unauthorized
- Infrastructure exceptions → 500 Internal Server Error

**Logging Guidelines**:
- Log exceptions with appropriate level (Warning for expected, Error for unexpected)
- Include correlation IDs in all exception logs
- Log contextual properties (JobId, DocumentId, etc.)
- Use structured logging with LoggerMessage source generators

## Anti-Patterns to Avoid

**DON'T return boolean for errors**:

```csharp
// ❌ BAD: Caller can't distinguish between error types
public async Task<bool> StartProcessingAsync(Guid jobId)
{
    ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
    if (job == null) return false;  // Job not found
    if (job.Status != Pending) return false;  // Invalid state
    // ...
}
```

**DO throw specific exceptions**:

```csharp
// ✅ GOOD: Clear, specific error types
public async Task StartProcessingAsync(Guid jobId)
{
    ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
    if (job == null) throw new JobNotFoundException(jobId);
    if (job.Status != Pending)
        throw new InvalidStateTransitionException(jobId, job.Status, Processing);
    // ...
}
```

**DON'T catch and swallow exceptions without good reason**:

```csharp
// ❌ BAD: Loses error information
try
{
    await _jobService.StartProcessingAsync(jobId);
}
catch (Exception)
{
    return false;  // What went wrong?
}
```

**DO let exceptions propagate**:

```csharp
// ✅ GOOD: Let Durable Functions handle it
await _jobService.StartProcessingAsync(jobId);
// Exception propagates with full context
```

## Exception Hierarchy

Organize exceptions by domain area:

```
src/common/DocProcessing.Domain/Exceptions/
├── Jobs/
│   ├── JobNotFoundException.cs
│   ├── InvalidStateTransitionException.cs
│   └── JobAlreadyProcessingException.cs
├── Documents/
│   ├── DocumentNotFoundException.cs
│   └── DocumentValidationException.cs
└── Processing/
    ├── OcrFailedException.cs
    └── ExtractionFailedException.cs
```

**When to create base exception classes**:
- When you need to catch multiple related exceptions
- When exceptions share common properties
- Example: `ProcessingException` base class for all processing-related errors

**Prefer specific exceptions**: Use `JobNotFoundException` over generic `NotFoundException` for better discoverability and type safety.
