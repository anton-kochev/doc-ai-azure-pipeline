using Worker.Orchestrator.Models;

namespace Worker.Orchestrator.Validation;

/// <summary>
/// Validates incoming Service Bus messages for document processing.
/// </summary>
public static class MessageValidator
{
    private static readonly string[] SupportedVersions = ["1.0"];

    /// <summary>
    /// Validates that the message has the required fields and correct structure.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <returns>A tuple indicating if validation passed and any error message.</returns>
    public static (bool IsValid, string? ErrorMessage) Validate(ProcessDocumentMessage? message)
    {
        if (message == null)
        {
            return (false, "Message is null or could not be deserialized");
        }

        // Validate version
        if (string.IsNullOrWhiteSpace(message.Version))
        {
            return (false, "Message version is required");
        }

        if (!SupportedVersions.Contains(message.Version))
        {
            return (false, $"Unsupported message version: {message.Version}. Supported versions: {string.Join(", ", SupportedVersions)}");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(message.JobId))
        {
            return (false, "JobId is required");
        }

        if (string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            return (false, "CorrelationId is required");
        }

        return (true, null);
    }
}
