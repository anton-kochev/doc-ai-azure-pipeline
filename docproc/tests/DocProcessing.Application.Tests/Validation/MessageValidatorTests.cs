using DocProcessing.Application.Models;
using DocProcessing.Application.Validation;

namespace DocProcessing.Application.Tests.Validation;

/// <summary>
/// Unit tests for MessageValidator to ensure proper validation of ProcessDocumentMessage.
/// The message contract should only require the minimal fields needed to start orchestration:
/// - Version: Message schema version
/// - JobId: Identifier to retrieve job details from database
/// - CorrelationId: For distributed tracing
///
/// All other information (DocumentId, BlobContainer, BlobPath, TenantId, etc.)
/// will be retrieved from the database during orchestration.
/// </summary>
public class MessageValidatorTests
{
    #region Valid Message Tests

    [Test]
    public async Task Validate_WithMinimalRequiredFields_ReturnsTrue()
    {
        // Arrange - Only the essential fields needed for orchestration
        // The orchestrator will fetch Document and Job details from database using JobId
        ProcessDocumentMessage message = new()
        {
            Version = "1.0",
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsTrue();
        await Assert.That(errorMessage).IsNull();
    }

    #endregion

    #region Null and Empty Message Tests

    [Test]
    public async Task Validate_WithNullMessage_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage? message = null;

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("Message is null or could not be deserialized");
    }

    #endregion

    #region Version Tests

    [Test]
    public async Task Validate_WithNullVersion_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = null!,
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("Message version is required");
    }

    [Test]
    public async Task Validate_WithEmptyVersion_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "",
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("Message version is required");
    }

    [Test]
    public async Task Validate_WithWhitespaceVersion_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "   ",
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("Message version is required");
    }

    [Test]
    public async Task Validate_WithUnsupportedVersion_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "2.0",
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).Contains("Unsupported message version: 2.0");
    }

    #endregion

    #region JobId Tests

    [Test]
    public async Task Validate_WithNullJobId_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "1.0",
            JobId = null!,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("JobId is required");
    }

    [Test]
    public async Task Validate_WithEmptyJobId_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "1.0",
            JobId = "",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("JobId is required");
    }

    [Test]
    public async Task Validate_WithWhitespaceJobId_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "1.0",
            JobId = "   ",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("JobId is required");
    }

    #endregion

    #region CorrelationId Tests

    [Test]
    public async Task Validate_WithNullCorrelationId_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "1.0",
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = null!
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("CorrelationId is required");
    }

    [Test]
    public async Task Validate_WithEmptyCorrelationId_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "1.0",
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = ""
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("CorrelationId is required");
    }

    [Test]
    public async Task Validate_WithWhitespaceCorrelationId_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage message = new()
        {
            Version = "1.0",
            JobId = Guid.NewGuid().ToString(),
            CorrelationId = "   "
        };

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errorMessage).IsEqualTo("CorrelationId is required");
    }

    #endregion
}
