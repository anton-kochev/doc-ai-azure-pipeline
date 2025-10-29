using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DocProcessing.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Infrastructure.MessageBroker.ServiceBus;

/// <summary>
/// Implementation of Service Bus message sending operations.
/// </summary>
public sealed partial class ServiceBusService : IMessagingService, IAsyncDisposable
{
    private readonly ILogger<ServiceBusService> _logger;
    private readonly IServiceBusSender _sender;
    private readonly TimeProvider _timeProvider;

    public ServiceBusService(
        ILogger<ServiceBusService> logger,
        IOptions<ServiceBusOptions> options,
        TimeProvider timeProvider,
        IServiceBusSenderFactory senderFactory)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        ServiceBusOptions optionsValue = options.Value;

        // Validate configuration
        if (string.IsNullOrWhiteSpace(optionsValue.QueueName))
        {
            throw new InvalidOperationException("ServiceBus:QueueName is not configured");
        }

        _sender = senderFactory.CreateSender(optionsValue.QueueName);
    }

    /// <inheritdoc />
    /// <exception cref="Azure.Messaging.ServiceBus.ServiceBusException">
    /// Thrown when the Service Bus operation fails.
    /// </exception>
    public async Task EnqueueJobAsync(
        Guid jobId, 
        string correlationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messagePayload = new
            {
                version = "1.0",
                jobId,
                correlationId,
                enqueuedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
            };

            string messageBody = JsonSerializer.Serialize(messagePayload);

            // Create a Service Bus message
            ServiceBusMessage message = new(messageBody)
            {
                MessageId = jobId.ToString(),
                CorrelationId = correlationId,
                ContentType = "application/json"
            };

            // Add custom properties for filtering/routing
            message.ApplicationProperties.Add("JobId", jobId.ToString());

            await _sender.SendMessageAsync(message, cancellationToken);

            LogJobMessageEnqueued(correlationId, jobId);
        }
        catch (Exception ex)
        {
            LogFailedToEnqueueJobMessage(ex, correlationId, jobId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Successfully enqueued job message. CorrelationId: {CorrelationId}, JobId: {JobId}")]
    private partial void LogJobMessageEnqueued(string correlationId, Guid jobId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Failed to enqueue job message. CorrelationId: {CorrelationId}, JobId: {JobId}")]
    private partial void LogFailedToEnqueueJobMessage(Exception exception, string correlationId, Guid jobId);
}
