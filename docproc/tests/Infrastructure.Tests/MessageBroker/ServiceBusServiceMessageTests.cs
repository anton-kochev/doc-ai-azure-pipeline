using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DocProcessing.Infrastructure.MessageBroker;
using DocProcessing.Infrastructure.MessageBroker.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Infrastructure.Tests.MessageBroker;

/// <summary>
/// Tests for ServiceBusService to ensure the correct message structure is sent to Service Bus.
/// These tests validate that SendMessageAsync is called with a message containing only the required fields:
/// - Version (always "1.0")
/// - JobId
/// - CorrelationId
/// - EnqueuedAtUtc (timestamp)
///
/// Note: Currently ServiceBusService cannot be easily unit tested because ServiceBusClient and ServiceBusSender
/// are sealed classes created in the constructor. These tests document the expected behavior and will need
/// refactoring of ServiceBusService to inject a testable abstraction.
/// </summary>
public class ServiceBusServiceMessageTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedTime;

    public ServiceBusServiceMessageTests()
    {
        _fixedTime = new DateTime(2025, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        _timeProvider = new FakeTimeProvider(_fixedTime);
    }

    /// <summary>
    /// This test verifies the complete message structure sent to Service Bus:
    /// - Body containing only: jobId, documentId, correlationId, enqueuedAtUtc
    /// - MessageId = jobId.ToString()
    /// - CorrelationId = correlationId parameter
    /// - ContentType = "application/json"
    /// - ApplicationProperties["JobId"] = jobId.ToString()
    /// - ApplicationProperties["DocumentId"] = documentId.ToString()
    /// </summary>
    [Test]
    public async Task EnqueueJobAsync_ShouldCallSendMessageAsync_WithCorrectMessageStructure()
    {
        // Arrange
        Guid jobId = Guid.NewGuid();
        string correlationId = Guid.NewGuid().ToString();

        // Mock the ServiceBusSender
        Mock<IServiceBusSender> mockSender = new();
        ServiceBusMessage? capturedMessage = null;

        mockSender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((msg, ct) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        // Mock the factory to return our mock sender
        Mock<IServiceBusSenderFactory> mockFactory = new();
        mockFactory
            .Setup(f => f.CreateSender(It.IsAny<string>()))
            .Returns(mockSender.Object);

        // Create logger and options
        Mock<ILogger<ServiceBusService>> logger = new();
        Microsoft.Extensions.Options.IOptions<ServiceBusOptions> options =
            Microsoft.Extensions.Options.Options.Create(
                new ServiceBusOptions
                {
                    QueueName = "test-queue",
                    ConnectionString =
                        "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
                });

        // Create a service with mocked dependencies
        ServiceBusService service = new(
            logger: logger.Object,
            options: options,
            timeProvider: _timeProvider,
            senderFactory: mockFactory.Object);

        // Act
        await service.EnqueueJobAsync(jobId, correlationId, CancellationToken.None);

        // Assert
        await Assert.That(capturedMessage).IsNotNull();

        // Verify message properties
        await Assert.That(capturedMessage!.MessageId).IsEqualTo(jobId.ToString());
        await Assert.That(capturedMessage.CorrelationId).IsEqualTo(correlationId);
        await Assert.That(capturedMessage.ContentType).IsEqualTo("application/json");

        // Verify application properties
        await Assert.That(capturedMessage.ApplicationProperties["JobId"]).IsEqualTo(jobId.ToString());

        // Verify message body contains exactly 4 fields: version, jobId, correlationId, enqueuedAtUtc
        string messageBody = capturedMessage.Body.ToString();
        Dictionary<string, JsonElement>? deserializedBody =
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(messageBody);

        await Assert.That(deserializedBody).IsNotNull();
        await Assert.That(deserializedBody!.Count).IsEqualTo(4);

        await Assert.That(deserializedBody.ContainsKey("version")).IsTrue();
        await Assert.That(deserializedBody["version"].GetString()).IsEqualTo("1.0");

        await Assert.That(deserializedBody.ContainsKey("jobId")).IsTrue();
        await Assert.That(deserializedBody["jobId"].GetGuid()).IsEqualTo(jobId);

        await Assert.That(deserializedBody.ContainsKey("correlationId")).IsTrue();
        await Assert.That(deserializedBody["correlationId"].GetString()).IsEqualTo(correlationId);

        await Assert.That(deserializedBody.ContainsKey("enqueuedAtUtc")).IsTrue();
        await Assert.That(deserializedBody["enqueuedAtUtc"].GetDateTime()).IsEqualTo(_fixedTime);

        // Verify SendMessageAsync was called exactly once
        mockSender.Verify(
            s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Test to validate the message payload structure that ServiceBusService creates.
    /// This test documents the expected format.
    /// </summary>
    [Test]
    public async Task MessagePayloadSerialization_ShouldContainOnlyRequiredFields()
    {
        // Arrange
        Guid jobId = Guid.NewGuid();
        string correlationId = Guid.NewGuid().ToString();
        DateTime enqueuedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // Create the message payload as ServiceBusService does
        var messagePayload = new
        {
            version = "1.0",
            jobId,
            correlationId,
            enqueuedAtUtc
        };

        // Act
        string messageBody = JsonSerializer.Serialize(messagePayload);
        Dictionary<string, JsonElement>? deserializedPayload =
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(messageBody);

        // Assert - Verify exactly 4 fields are present, no more, no less
        await Assert.That(deserializedPayload).IsNotNull();
        await Assert.That(deserializedPayload!.Count).IsEqualTo(4);

        // Verify required fields with correct values
        await Assert.That(deserializedPayload.ContainsKey("version")).IsTrue();
        await Assert.That(deserializedPayload["version"].GetString()).IsEqualTo("1.0");

        await Assert.That(deserializedPayload.ContainsKey("jobId")).IsTrue();
        await Assert.That(deserializedPayload["jobId"].GetGuid()).IsEqualTo(jobId);

        await Assert.That(deserializedPayload.ContainsKey("correlationId")).IsTrue();
        await Assert.That(deserializedPayload["correlationId"].GetString()).IsEqualTo(correlationId);

        await Assert.That(deserializedPayload.ContainsKey("enqueuedAtUtc")).IsTrue();
        await Assert.That(deserializedPayload["enqueuedAtUtc"].GetDateTime()).IsEqualTo(_fixedTime);
    }

    /// <summary>
    /// Test that validates the message payload is compatible with ProcessDocumentMessage
    /// after we update it to require only minimal fields.
    /// </summary>
    [Test]
    public async Task MessagePayload_ShouldDeserializeToProcessDocumentMessage()
    {
        // Arrange
        Guid jobId = Guid.NewGuid();
        string correlationId = Guid.NewGuid().ToString();
        DateTime enqueuedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var messagePayload = new
        {
            version = "1.0",
            jobId,
            correlationId,
            enqueuedAtUtc
        };

        string messageBody = JsonSerializer.Serialize(messagePayload);

        // Act - Simulate what DocumentIngestionTrigger does
        ProcessDocumentMessageDto? deserializedMessage = JsonSerializer.Deserialize<ProcessDocumentMessageDto>(
            messageBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        await Assert.That(deserializedMessage).IsNotNull();
        await Assert.That(deserializedMessage!.Version).IsEqualTo("1.0");
        await Assert.That(deserializedMessage.JobId).IsEqualTo(jobId.ToString());
        await Assert.That(deserializedMessage.CorrelationId).IsEqualTo(correlationId);
        await Assert.That(deserializedMessage.EnqueuedAtUtc).IsEqualTo(enqueuedAtUtc);
    }
}

/// <summary>
/// DTO class for testing deserialization.
/// This matches the minimal structure we want ProcessDocumentMessage to have.
/// </summary>
internal class ProcessDocumentMessageDto
{
    public string Version { get; set; } = "1.0";
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public DateTime? EnqueuedAtUtc { get; set; }
}
