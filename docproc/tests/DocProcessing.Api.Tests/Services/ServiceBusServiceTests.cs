using DocProcessing.Infrastructure.MessageBroker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Api.Tests.Services;

public class ServiceBusServiceTests
{
    private readonly Mock<ILogger<ServiceBusService>> _loggerMock;

    public ServiceBusServiceTests()
    {
        _loggerMock = new Mock<ILogger<ServiceBusService>>();

        // Setup logger mock to support source-generated logging
        _loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullQueueName_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = null!,
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(_loggerMock.Object, options));

        Assert.Equal("ServiceBus:QueueName is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyQueueName_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(_loggerMock.Object, options));

        Assert.Equal("ServiceBus:QueueName is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceQueueName_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "   ",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(_loggerMock.Object, options));

        Assert.Equal("ServiceBus:QueueName is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithNoConnectionStringOrNamespace_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            ConnectionString = null,
            Namespace = ""
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(_loggerMock.Object, options));

        Assert.Equal("Either ServiceBus:ConnectionString or ServiceBus:Namespace must be configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyConnectionStringAndEmptyNamespace_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            ConnectionString = "",
            Namespace = ""
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(_loggerMock.Object, options));

        Assert.Equal("Either ServiceBus:ConnectionString or ServiceBus:Namespace must be configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceConnectionStringAndWhitespaceNamespace_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            ConnectionString = "   ",
            Namespace = "   "
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(_loggerMock.Object, options));

        Assert.Equal("Either ServiceBus:ConnectionString or ServiceBus:Namespace must be configured", exception.Message);
    }

    [Fact]
    public async Task Constructor_LogsConnectionStringInitialization_WhenConnectionStringProvided()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test123=="
        });

        // Act
        await using ServiceBusService service = new(_loggerMock.Object, options);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Initializing Service Bus client with connection string")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Constructor_LogsSenderCreation_WhenConnectionStringProvided()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test123=="
        });

        // Act
        await using ServiceBusService service = new(_loggerMock.Object, options);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Service Bus sender created for queue: test-queue")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Constructor_LogsManagedIdentityInitialization_WhenNamespaceProvidedAndNoConnectionString()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            Namespace = "test.servicebus.windows.net"
        });

        // Act & Assert - This will fail because DefaultAzureCredential won't work in test environment
        // but we can verify the logging happens before the client creation fails
        try
        {
            await using ServiceBusService service = new(_loggerMock.Object, options);
        }
        catch
        {
            // Expected to fail in test environment
        }

        // Assert - verify the logging happened
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Initializing Service Bus client with Managed Identity for namespace: test.servicebus.windows.net")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region EnqueueJobAsync Tests

    // NOTE: The following tests demonstrate what should be tested for EnqueueJobAsync.
    // However, due to the current implementation where ServiceBusClient and ServiceBusSender
    // are created in the constructor (sealed classes that cannot be easily mocked),
    // true unit testing of EnqueueJobAsync requires refactoring.
    //
    // Recommended refactoring approaches:
    // 1. Inject ServiceBusClient as a dependency (with interface or factory pattern)
    // 2. Create a testable wrapper/facade around Azure Service Bus operations
    // 3. Use the Strategy pattern to make the sender implementation swappable
    //
    // For now, these test cases serve as documentation for what should be tested
    // once the refactoring is complete.

    /*
    [Fact]
    public async Task EnqueueJobAsync_WithValidParameters_SendsMessageSuccessfully()
    {
        // This test would verify:
        // - Message is sent with correct MessageId (jobId as string)
        // - Message has correct CorrelationId
        // - Message has correct ContentType (application/json)
        // - ApplicationProperties contain JobId and DocumentId
        // - Message body is correctly serialized JSON
        // - Success is logged
    }

    [Fact]
    public async Task EnqueueJobAsync_WithValidParameters_CreatesCorrectMessagePayload()
    {
        // This test would verify:
        // - jobId is included in the payload
        // - documentId is included in the payload
        // - correlationId is included in the payload
        // - enqueuedAtUtc is set and close to DateTime.UtcNow
    }

    [Fact]
    public async Task EnqueueJobAsync_WhenSendFails_LogsErrorAndRethrows()
    {
        // This test would verify:
        // - Exception is logged with appropriate context (JobId, DocumentId)
        // - Exception is re-thrown
    }

    [Fact]
    public async Task EnqueueJobAsync_SetsMessageProperties_Correctly()
    {
        // This test would verify:
        // - MessageId equals jobId.ToString()
        // - CorrelationId equals the provided correlationId parameter
        // - ContentType equals "application/json"
    }

    [Fact]
    public async Task EnqueueJobAsync_SetsApplicationProperties_Correctly()
    {
        // This test would verify:
        // - ApplicationProperties["JobId"] equals jobId.ToString()
        // - ApplicationProperties["DocumentId"] equals documentId.ToString()
    }
    */

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test123=="
        });

        ServiceBusService service = new(_loggerMock.Object, options);

        // Act & Assert - Should not throw
        await service.DisposeAsync();
        await service.DisposeAsync(); // Second call should also succeed
    }

    #endregion
}
