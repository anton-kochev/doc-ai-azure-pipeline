using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace DocProcessing.TestUtilities.Logging;

/// <summary>
/// Extension methods for FakeLogger to simplify test assertions.
/// </summary>
public static class FakeLoggerExtensions
{
    /// <summary>
    /// Verifies that a log entry was recorded with the specified log level and message substring.
    /// </summary>
    /// <typeparam name="T">The logger category type.</typeparam>
    /// <param name="fakeLogger">The fake logger instance.</param>
    /// <param name="logLevel">The expected log level.</param>
    /// <param name="message">A substring that should be contained in the log message (case-insensitive).</param>
    /// <exception cref="Xunit.Sdk.XunitException">Thrown when no matching log entry is found.</exception>
    public static void VerifyWasCalled<T>(this FakeLogger<T> fakeLogger, LogLevel logLevel, string message)
    {
        var hasLogRecord = fakeLogger
            .Collector
            .GetSnapshot()
            .Any(log => log.Level == logLevel
                        && log.Message.Contains(message, StringComparison.OrdinalIgnoreCase));

        if (hasLogRecord)
        {
            return;
        }

        var exceptionMessage = $"Expected log entry with level [{logLevel}] and message containing '{message}' not found."
            + Environment.NewLine
            + $"Log entries found:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, fakeLogger.Collector.GetSnapshot().Select(l => l));

        throw new Xunit.Sdk.XunitException(exceptionMessage);
    }
}
