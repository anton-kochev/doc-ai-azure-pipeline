using DocProcessing.Application.Models;
using DocProcessing.Application.Validation;
using Xunit;

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

    [Fact]
    public void Validate_WithMinimalRequiredFields_ReturnsTrue()
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
        Assert.True(isValid, $"Validation should pass with minimal required fields. Error: {errorMessage}");
        Assert.Null(errorMessage);
    }

    #endregion

    #region Null and Empty Message Tests

    [Fact]
    public void Validate_WithNullMessage_ReturnsFalse()
    {
        // Arrange
        ProcessDocumentMessage? message = null;

        // Act
        (bool isValid, string? errorMessage) = MessageValidator.Validate(message);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Message is null or could not be deserialized", errorMessage);
    }

    #endregion

    #region Version Tests

    [Fact]
    public void Validate_WithNullVersion_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("Message version is required", errorMessage);
    }

    [Fact]
    public void Validate_WithEmptyVersion_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("Message version is required", errorMessage);
    }

    [Fact]
    public void Validate_WithWhitespaceVersion_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("Message version is required", errorMessage);
    }

    [Fact]
    public void Validate_WithUnsupportedVersion_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Contains("Unsupported message version: 2.0", errorMessage);
    }

    #endregion

    #region JobId Tests

    [Fact]
    public void Validate_WithNullJobId_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("JobId is required", errorMessage);
    }

    [Fact]
    public void Validate_WithEmptyJobId_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("JobId is required", errorMessage);
    }

    [Fact]
    public void Validate_WithWhitespaceJobId_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("JobId is required", errorMessage);
    }

    #endregion

    #region CorrelationId Tests

    [Fact]
    public void Validate_WithNullCorrelationId_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("CorrelationId is required", errorMessage);
    }

    [Fact]
    public void Validate_WithEmptyCorrelationId_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("CorrelationId is required", errorMessage);
    }

    [Fact]
    public void Validate_WithWhitespaceCorrelationId_ReturnsFalse()
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
        Assert.False(isValid);
        Assert.Equal("CorrelationId is required", errorMessage);
    }

    #endregion
}
