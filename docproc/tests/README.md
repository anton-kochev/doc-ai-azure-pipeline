# Testing Guide

This guide covers testing practices for the DocProcessing solution, focusing on testing source-generated logging.

## Choosing the Right Logger for Tests

The solution uses **compile-time logging source generation** via `[LoggerMessage]` attributes for better performance. Choose the appropriate logger based on whether you need to test logging behavior:

| Logger Type | Use When | Example |
|-------------|----------|---------|
| **`FakeLogger<T>`** | Testing logging behavior (asserting on log messages) | `_logger.VerifyWasCalled(LogLevel.Information, "Job created")` |
| **`NullLogger<T>`** | Logger is only a dependency (no logging assertions) | `new MyService(NullLogger<MyService>.Instance)` |

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
