using DocProcessing.Infrastructure.MessageBroker;
using DocProcessing.Infrastructure.MessageBroker.ServiceBus;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DocProcessing.Api.Tests.Services;

public class ServiceBusServiceTests
{
    private FakeLogger<ServiceBusService> CreateLogger()
    {
        return new FakeLogger<ServiceBusService>();
    }

    private FakeTimeProvider CreateTimeProvider()
    {
        return new FakeTimeProvider();
    }

    private Mock<IServiceBusSenderFactory> CreateMockFactory()
    {
        Mock<IServiceBusSender> mockSender = new();
        Mock<IServiceBusSenderFactory> mockFactory = new();
        mockFactory.Setup(f => f.CreateSender(It.IsAny<string>()))
            .Returns(mockSender.Object);
        return mockFactory;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullQueueName_ThrowsInvalidOperationException()
    {
        // Arrange
        FakeLogger<ServiceBusService> logger = CreateLogger();
        FakeTimeProvider timeProvider = CreateTimeProvider();
        Mock<IServiceBusSenderFactory> factory = CreateMockFactory();
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = null!,
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(logger, options, timeProvider, factory.Object));

        Assert.Equal("ServiceBus:QueueName is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyQueueName_ThrowsInvalidOperationException()
    {
        // Arrange
        FakeLogger<ServiceBusService> logger = CreateLogger();
        FakeTimeProvider timeProvider = CreateTimeProvider();
        Mock<IServiceBusSenderFactory> factory = CreateMockFactory();
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(logger, options, timeProvider, factory.Object));

        Assert.Equal("ServiceBus:QueueName is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceQueueName_ThrowsInvalidOperationException()
    {
        // Arrange
        FakeLogger<ServiceBusService> logger = CreateLogger();
        FakeTimeProvider timeProvider = CreateTimeProvider();
        Mock<IServiceBusSenderFactory> factory = CreateMockFactory();
        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "   ",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
        });

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceBusService(logger, options, timeProvider, factory.Object));

        Assert.Equal("ServiceBus:QueueName is not configured", exception.Message);
    }

    #endregion

    #region EnqueueJobAsync Tests

    // These tests are now covered in Infrastructure.Tests/MessageBroker/ServiceBusServiceMessageTests.cs
    // which properly mocks IServiceBusSender to test the complete message flow

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_DisposesTheSender()
    {
        // Arrange
        FakeLogger<ServiceBusService> logger = CreateLogger();
        FakeTimeProvider timeProvider = CreateTimeProvider();
        Mock<IServiceBusSender> mockSender = new();
        Mock<IServiceBusSenderFactory> mockFactory = new();
        mockFactory.Setup(f => f.CreateSender(It.IsAny<string>()))
            .Returns(mockSender.Object);

        IOptions<ServiceBusOptions> options = Options.Create(new ServiceBusOptions
        {
            QueueName = "test-queue",
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test123=="
        });

        ServiceBusService service = new(logger, options, timeProvider, mockFactory.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        mockSender.Verify(s => s.DisposeAsync(), Times.Once);
    }

    #endregion
}
