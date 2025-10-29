using DocProcessing.Application.Models;

namespace DocProcessing.Application.Validation;

/// <summary>
/// Validates incoming Service Bus messages for document processing.
/// </summary>
public static class MessageValidator
{
    private static readonly string[] SupportedVersions = ["1.0"];

    /// <summary>
    /// Validates that the message has the required fields and correct structure.
    /// Only validates the minimal fields needed to start orchestration:
    /// - Version: Message schema version
    /// - JobId: To retrieve job details from database
    /// - CorrelationId: For distributed tracing
    ///
    /// All other fields (DocumentId, BlobContainer, BlobPath, TenantId, etc.)
    /// are optional and will be retrieved during orchestration.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <returns>A tuple indicating if validation passed and any error message.</returns>
    public static (bool IsValid, string? ErrorMessage) Validate(ProcessDocumentMessage? message)
    {
        if (message == null)
        {
            return (false, "Message is null or could not be deserialized");
        }

        if (string.IsNullOrWhiteSpace(message.Version))
        {
            return (false, "Message version is required");
        }

        if (!SupportedVersions.Contains(message.Version))
        {
            return (false, $"Unsupported message version: {message.Version}. Supported versions: {string.Join(", ", SupportedVersions)}");
        }

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
