# Testing Guide

This guide covers testing practices for the DocProcessing solution, focusing on testing source-generated logging.

## Testing Source-Generated Logging

The solution uses **compile-time logging source generation** via `[LoggerMessage]` attributes for better performance. Use `FakeLogger<T>` from `Microsoft.Extensions.Diagnostics.Testing` to test logging behavior.

### Setup

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

### Basic Usage

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

### Key Principles

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

### VerifyWasCalled Extension Method

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

### Advanced Scenarios

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
- **Infrastructure.Tests/Services/ProcessJobServiceTests.cs** - Shows all patterns and best practices

## Why FakeLogger Over Moq?

Traditional Moq-based logger mocking doesn't work with source-generated logging:

```csharp
// ❌ Doesn't work with [LoggerMessage] attributes
_loggerMock.Verify(x => x.Log(...), Times.Once);

// ✅ Works correctly with source-generated logging
_logger.VerifyWasCalled(LogLevel.Information, "message");
```

Source-generated logging uses optimized internal methods that bypass the generic `Log()` method, so Moq can't verify the calls. `FakeLogger<T>` captures all log output correctly.
