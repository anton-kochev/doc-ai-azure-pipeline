# Testing Guide

This guide covers testing practices for the DocProcessing solution.

## Table of Contents

- [End-to-End Integration Tests](#end-to-end-integration-tests)
- [Shared Test Utilities](#shared-test-utilities)
- [Choosing the Right Logger for Tests](#choosing-the-right-logger-for-tests)
- [Testing ManualReview State Transitions](#testing-manualreview-state-transitions)

## End-to-End Integration Tests

The `DocProcessing.EndToEnd.Tests` project verifies full document processing pipeline flows (Upload → OCR → Preprocess → ... → Completed) without requiring Azure infrastructure. It uses real Application services against an in-memory database, with mocked external Azure services (OCR, Service Bus).

### Architecture

**`EndToEndTestFixture`** — each test creates its own fixture instance for full isolation. Wires:
- Real services: `DocumentService`, `ProcessJobService`, all `*StageActivity` classes, preprocessing services
- Stateful fakes: `InMemoryStorageService` (ConcurrentDictionary-based), `InMemoryDbContext`, `FakeTimeProvider`
- Mocks: `IOcrService`, `IMessagingService`

**`PipelineSimulator`** — simulates the orchestrator's stage loop (OCR → Preprocess → Embed → Extract → Validate → Persist → Notify). Supports resume from a specific stage index for manual review flows.

> **Documented divergence**: The simulator forwards stage output metadata between stages. Production currently rebuilds metadata fresh from the Document record per stage, which is a known bug tracked separately.

**`ControllableActivityFactory`** — wraps `IPipelineActivityFactory`, delegates to real factory by default. Tests can override specific stages with mock activities to inject failures or manual review triggers.

**`UploadRequestBuilder`** — fluent builder replicating the upload flow: compute SHA256 → upload to storage → get/create document → get/create job → enqueue if new.

**`OcrResultBuilder`** — fluent builder for creating test `OcrResult` instances with configurable page count, confidence, and text content.

### Test Classes (5 classes, 24 tests)

| Class | Tests | Coverage |
|---|---|---|
| `HappyPathFlowTests` | 7 | Full pipeline success, OCR verification, metadata, timestamps, stage guard |
| `StageFailureFlowTests` | 4 | OCR failures, missing metadata, unexpected errors |
| `RetryFlowTests` | 3 | Retry resets, re-run succeeds with attempts=2, messaging |
| `ManualReviewFlowTests` | 5 | Manual review trigger, resume, skip-earlier-stages, reject, wrong state |
| `IdempotencyFlowTests` | 5 | Duplicate uploads, different tenants/profiles, terminal vs non-terminal |

### Running

```bash
# Run E2E tests only (from docproc/)
dotnet test --project tests/DocProcessing.EndToEnd.Tests/
```

### Known Limitations

- **EF Core InMemory** does not enforce unique constraints — idempotency tests rely on query-first-then-insert pattern
- **PipelineSimulator forwards metadata** (production doesn't yet) — explicitly documented divergence
- **No cancellation token or concurrent upload tests** in v1 — deferred

### Shared Test Utilities

`DocProcessing.TestUtilities` provides shared helpers used across all test projects:
- `Database/InMemoryDbContext` — EF Core in-memory database wrapper implementing `IApplicationDbContext` with `IDisposable` and `IAsyncDisposable`
- `Logging/FakeLoggerExtensions` — `VerifyWasCalled` extension for asserting on source-generated log messages

---

## Choosing the Right Logger for Tests

The solution uses **compile-time logging source generation** via `[LoggerMessage]` attributes for better performance. Choose the appropriate logger based on whether you need to test logging behavior:

| Logger Type         | Use When                                             | Example                                                        |
|---------------------|------------------------------------------------------|----------------------------------------------------------------|
| **`FakeLogger<T>`** | Testing logging behavior (asserting on log messages) | `_logger.VerifyWasCalled(LogLevel.Information, "Job created")` |
| **`NullLogger<T>`** | Logger is only a dependency (no logging assertions)  | `new MyService(NullLogger<MyService>.Instance)`                |

**Quick Rule:** If your test uses `_logger.VerifyWasCalled()`, `_logger.Collector`, or checks log content → use `FakeLogger<T>`. Otherwise → use `NullLogger<T>`.

### When to Use NullLogger<T>

Use `NullLogger<T>` when your tests don't verify logging behavior but the service requires an `ILogger<T>` dependency:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class BlobStorageServiceTests
{
    private readonly ILogger<BlobStorageService> _logger = NullLogger<BlobStorageService>.Instance;
    private readonly BlobStorageService _service;

    public BlobStorageServiceTests()
    {
        // Logger is passed to service but no logging is tested
        _service = new BlobStorageService(options, _logger);
    }

    [Fact]
    public async Task UploadAsync_WithValidFile_UploadsSuccessfully()
    {
        // Test focuses on upload behavior, not logging
        var result = await _service.UploadAsync("file.txt", stream);
        Assert.NotNull(result);
    }
}
```

**Benefits of NullLogger<T>:**
- Lightweight - no overhead from capturing logs
- Clear intent - signals that logging is not tested
- Built-in - available in `Microsoft.Extensions.Logging.Abstractions`
- Singleton - use `NullLogger<T>.Instance` for better performance

### When to Use FakeLogger<T>

Use `FakeLogger<T>` when you need to verify logging behavior in your tests.

#### Setup

Add the shared test utilities reference to your test project:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\tests\DocProcessing.TestUtilities\DocProcessing.TestUtilities.csproj" />
</ItemGroup>
```

Import the namespace:

```csharp
using DocProcessing.TestUtilities.Logging;
using Microsoft.Extensions.Logging.Testing;
```

#### Basic Usage

```csharp
public class MyServiceTests
{
    private readonly FakeLogger<MyService> _logger;
    private readonly MyService _service;

    public MyServiceTests()
    {
        _logger = new FakeLogger<MyService>();
        _service = new MyService(_logger);
    }

    [Fact]
    public async Task MyMethod_LogsExpectedMessage()
    {
        // Act
        await _service.DoSomething();

        // Assert - Verify log with level and message substring (case-insensitive)
        _logger.VerifyWasCalled(LogLevel.Information, "expected message");
    }
}
```

#### Key Principles

**1. Use Substring Matching, Not Exact Messages**

This makes tests resilient to log format changes:

```csharp
// ❌ Brittle - breaks when format changes
_logger.VerifyWasCalled(LogLevel.Information,
    "Job 12345678-1234-1234-1234-123456789012 transitioned to Processing. Attempts: 1");

// ✅ Resilient - tests the essential part
_logger.VerifyWasCalled(LogLevel.Information, "Job transitioned to Processing");
```

**2. Clear Logs Between Test Phases**

When testing multi-step workflows:

```csharp
[Fact]
public async Task MultiStepWorkflow_LogsEachStep()
{
    // Step 1
    await _service.CreateJob();
    _logger.VerifyWasCalled(LogLevel.Information, "Job created");

    // Clear logs before next step
    _logger.Collector.Clear();

    // Step 2
    await _service.ProcessJob();
    _logger.VerifyWasCalled(LogLevel.Information, "Job processing");
}
```

**3. Test Different Log Levels**

```csharp
// Information logs
_logger.VerifyWasCalled(LogLevel.Information, "Operation completed");

// Warning logs
_logger.VerifyWasCalled(LogLevel.Warning, "Job not found");

// Error logs
_logger.VerifyWasCalled(LogLevel.Error, "Operation failed");

// Debug logs
_logger.VerifyWasCalled(LogLevel.Debug, "Looking for existing job");
```

#### VerifyWasCalled Extension Method

The `VerifyWasCalled` extension method simplifies log assertions:

```csharp
public static void VerifyWasCalled<T>(
    this FakeLogger<T> fakeLogger,
    LogLevel logLevel,
    string message)
```

**Features:**
- Case-insensitive substring matching
- Helpful error messages showing all captured logs
- Works with source-generated logging (`[LoggerMessage]` attributes)

**When assertion fails**, you get a detailed error message:
```
Expected log entry with level [Information] and message containing 'Job created' not found.
Log entries found:
[Debug] Looking for existing job with idempotency key: abc123
[Warning] Job already exists
```

#### Advanced Scenarios

**Verify Log Parameters**

```csharp
[Fact]
public async Task ProcessJob_LogsJobId()
{
    var jobId = Guid.NewGuid();
    await _service.ProcessJob(jobId);

    // Verify JobId appears in the log message
    _logger.VerifyWasCalled(LogLevel.Information, jobId.ToString());
}
```

**Inspect Log Entries Directly**

For complex assertions, access the full log collection:

```csharp
[Fact]
public async Task ProcessMultipleItems_LogsAll()
{
    await _service.ProcessItems(new[] { "A", "B", "C" });

    var logs = _logger.Collector.GetSnapshot();
    Assert.Equal(3, logs.Count(log =>
        log.Level == LogLevel.Information &&
        log.Message.Contains("Processing item")));
}
```

## Testing ManualReview State Transitions

The ManualReview state machine enables human-in-the-loop workflows. Testing these transitions requires careful attention to state validation and logging.

### State Machine Overview

**Valid Transitions:**
```
Processing → ManualReview  (RequestManualReviewAsync)
ManualReview → Processing  (ResumeFromManualReviewAsync)
ManualReview → Failed      (RejectManualReviewAsync)
```

### Testing Service Methods

**1. RequestManualReviewAsync - Processing → ManualReview**

```csharp
[Fact]
public async Task RequestManualReviewAsync_TransitionsToManualReview()
{
    // Arrange
    var job = new ProcessJob
    {
        JobId = Guid.NewGuid(),
        Status = ProcessJobStatus.Processing,
        Stage = ProcessJobStage.Validate
    };
    await _dbContext.ProcessJobs.AddAsync(job);
    await _dbContext.SaveChangesAsync();

    // Act
    await _service.RequestManualReviewAsync(
        job.JobId,
        "Low confidence score",
        CancellationToken.None);

    // Assert
    var updated = await _dbContext.ProcessJobs.FindAsync(job.JobId);
    Assert.Equal(ProcessJobStatus.ManualReview, updated.Status);
    Assert.Equal("Low confidence score", updated.LastErrorMessage);
    Assert.NotNull(updated.CompletedAtUtc);

    // Verify logging
    _logger.VerifyWasCalled(LogLevel.Information, "Manual review requested");
}
```

**2. ResumeFromManualReviewAsync - ManualReview → Processing**

```csharp
[Fact]
public async Task ResumeFromManualReviewAsync_TransitionsToProcessing()
{
    // Arrange
    var job = new ProcessJob
    {
        JobId = Guid.NewGuid(),
        Status = ProcessJobStatus.ManualReview,
        Stage = ProcessJobStage.Validate,
        Attempts = 1
    };
    await _dbContext.ProcessJobs.AddAsync(job);
    await _dbContext.SaveChangesAsync();

    // Act
    await _service.ResumeFromManualReviewAsync(job.JobId, CancellationToken.None);

    // Assert
    var updated = await _dbContext.ProcessJobs.FindAsync(job.JobId);
    Assert.Equal(ProcessJobStatus.Processing, updated.Status);
    Assert.Null(updated.CompletedAtUtc); // Cleared on resume
    Assert.Equal(2, updated.Attempts); // Incremented

    _logger.VerifyWasCalled(LogLevel.Information, "Resumed from manual review");
}
```

**3. RejectManualReviewAsync - ManualReview → Failed**

```csharp
[Fact]
public async Task RejectManualReviewAsync_TransitionsToFailed()
{
    // Arrange
    var job = new ProcessJob
    {
        JobId = Guid.NewGuid(),
        Status = ProcessJobStatus.ManualReview
    };
    await _dbContext.ProcessJobs.AddAsync(job);
    await _dbContext.SaveChangesAsync();

    // Act
    await _service.RejectManualReviewAsync(
        job.JobId,
        "MANUAL_REJECTION",
        "Document quality insufficient",
        CancellationToken.None);

    // Assert
    var updated = await _dbContext.ProcessJobs.FindAsync(job.JobId);
    Assert.Equal(ProcessJobStatus.Failed, updated.Status);
    Assert.Equal("MANUAL_REJECTION", updated.LastErrorCode);
    Assert.Equal("Document quality insufficient", updated.LastErrorMessage);

    _logger.VerifyWasCalled(LogLevel.Warning, "Rejected during manual review");
}
```

### Testing Invalid Transitions

Always test that invalid state transitions throw `InvalidStateTransitionException`:

```csharp
[Fact]
public async Task RequestManualReviewAsync_FromCompleted_ThrowsException()
{
    // Arrange
    var job = new ProcessJob
    {
        JobId = Guid.NewGuid(),
        Status = ProcessJobStatus.Completed // Invalid source state
    };
    await _dbContext.ProcessJobs.AddAsync(job);
    await _dbContext.SaveChangesAsync();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<InvalidStateTransitionException>(
        () => _service.RequestManualReviewAsync(job.JobId, "reason"));

    Assert.Equal(job.JobId, exception.JobId);
    Assert.Equal(ProcessJobStatus.Completed, exception.CurrentStatus);
    Assert.Equal(ProcessJobStatus.ManualReview, exception.AttemptedStatus);

    // Verify warning logged
    _logger.VerifyWasCalled(LogLevel.Warning, "Invalid state transition");
}
```

### Testing State Validators

Test the centralized state transition validator:

```csharp
[Theory]
[InlineData(ProcessJobStatus.Processing, ProcessJobStatus.ManualReview, true)]
[InlineData(ProcessJobStatus.ManualReview, ProcessJobStatus.Processing, true)]
[InlineData(ProcessJobStatus.ManualReview, ProcessJobStatus.Failed, true)]
[InlineData(ProcessJobStatus.Pending, ProcessJobStatus.ManualReview, false)]
[InlineData(ProcessJobStatus.Completed, ProcessJobStatus.ManualReview, false)]
public void IsValidTransition_ManualReviewTransitions_ReturnsExpected(
    ProcessJobStatus from,
    ProcessJobStatus to,
    bool expectedValid)
{
    // Act
    var isValid = ProcessJobStatusTransitions.IsValidTransition(from, to);

    // Assert
    Assert.Equal(expectedValid, isValid);
}
```

### Key Testing Patterns

1. **Test State Transitions**: Verify status changes correctly
2. **Test Field Updates**: Check CompletedAtUtc, LastErrorCode, LastErrorMessage, Attempts
3. **Test Logging**: Use `FakeLogger<T>` to verify appropriate log levels
4. **Test Exceptions**: Verify invalid transitions throw `InvalidStateTransitionException`
5. **Test Not Found**: Verify `JobNotFoundException` when job doesn't exist

### Examples

See comprehensive test examples in:
- **ManualReview Service Tests**: `Infrastructure.Tests/Services/ProcessJobServiceTests.cs` (lines 600-900)
  - RequestManualReviewAsync: 9 tests
  - ResumeFromManualReviewAsync: 9 tests
  - RejectManualReviewAsync: 9 tests
- **State Validator Tests**: `Infrastructure.Tests/Validation/ProcessJobStatusTransitionsTests.cs` (19 tests)

## Examples

See comprehensive examples in:
- **FakeLogger<T> usage**: `Infrastructure.Tests/Services/ProcessJobServiceTests.cs` - Tests logging behavior with assertions
- **NullLogger<T> usage**: `DocProcessing.Api.Tests/Services/BlobStorageServiceTests.cs` - Logger as dependency only

## Why FakeLogger Over Moq?

Traditional Moq-based logger mocking doesn't work with source-generated logging:

```csharp
// ❌ Doesn't work with [LoggerMessage] attributes
_loggerMock.Verify(x => x.Log(...), Times.Once);

// ✅ Works correctly with source-generated logging
_logger.VerifyWasCalled(LogLevel.Information, "message");
```

Source-generated logging uses optimized internal methods that bypass the generic `Log()` method, so Moq can't verify the calls. `FakeLogger<T>` captures all log output correctly.
