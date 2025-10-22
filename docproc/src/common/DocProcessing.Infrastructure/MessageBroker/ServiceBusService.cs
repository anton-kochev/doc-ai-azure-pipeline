using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using DocProcessing.Application.Interfaces;

namespace DocProcessing.Infrastructure.MessageBroker;

/// <summary>
/// Implementation of Service Bus message sending operations.
/// </summary>
public sealed partial class ServiceBusService : IMessagingService, IAsyncDisposable
{
    private readonly ILogger<ServiceBusService> _logger;
    private readonly ServiceBusOptions _options;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusService(
        ILogger<ServiceBusService> logger,
        IOptions<ServiceBusOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Validate configuration
        if (string.IsNullOrWhiteSpace(_options.QueueName))
        {
            throw new InvalidOperationException("ServiceBus:QueueName is not configured");
        }

        // Configure retry options for resilience against transient failures
        ServiceBusClientOptions clientOptions = new()
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                Mode = ServiceBusRetryMode.Exponential,
                MaxRetries = _options.MaxRetries,
                Delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds),
                MaxDelay = TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds),
                TryTimeout = TimeSpan.FromSeconds(_options.TryTimeoutSeconds)
            }
        };

        LogServiceBusRetryPolicy(_options.MaxRetries, _options.RetryDelaySeconds, _options.MaxRetryDelaySeconds, _options.TryTimeoutSeconds);

        // Create a Service Bus client using connection string or Managed Identity
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            LogInitializingServiceBusWithConnectionString();
            _client = new ServiceBusClient(_options.ConnectionString, clientOptions);
        }
        else if (!string.IsNullOrWhiteSpace(_options.Namespace))
        {
            LogInitializingServiceBusWithManagedIdentity(_options.Namespace);
            _client = new ServiceBusClient(_options.Namespace, new DefaultAzureCredential(), clientOptions);
        }
        else
        {
            throw new InvalidOperationException("Either ServiceBus:ConnectionString or ServiceBus:Namespace must be configured");
        }

        _sender = _client.CreateSender(_options.QueueName);
        LogServiceBusSenderCreated(_options.QueueName);
    }

    /// <inheritdoc />
    /// <exception cref="Azure.Messaging.ServiceBus.ServiceBusException">
    /// Thrown when the Service Bus operation fails.
    /// </exception>
    public async Task EnqueueJobAsync(Guid jobId, Guid documentId, string correlationId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create message payload
            var messagePayload = new
            {
                jobId,
                documentId,
                correlationId,
                enqueuedAtUtc = DateTime.UtcNow
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
            message.ApplicationProperties.Add("DocumentId", documentId.ToString());

            // Send the message
            await _sender.SendMessageAsync(message, cancellationToken);

            LogJobMessageEnqueued(jobId, documentId, correlationId);
        }
        catch (Exception ex)
        {
            LogFailedToEnqueueJobMessage(ex, jobId, documentId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Configured Service Bus retry policy: MaxRetries={MaxRetries}, Delay={Delay}s, MaxDelay={MaxDelay}s, TryTimeout={TryTimeout}s")]
    private partial void LogServiceBusRetryPolicy(int maxRetries, int delay, int maxDelay, int tryTimeout);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Initializing Service Bus client with connection string")]
    private partial void LogInitializingServiceBusWithConnectionString();

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Initializing Service Bus client with Managed Identity for namespace: {Namespace}")]
    private partial void LogInitializingServiceBusWithManagedIdentity(string @namespace);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Service Bus sender created for queue: {QueueName}")]
    private partial void LogServiceBusSenderCreated(string queueName);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Successfully enqueued job message: JobId={JobId}, DocumentId={DocumentId}, CorrelationId={CorrelationId}")]
    private partial void LogJobMessageEnqueued(Guid jobId, Guid documentId, string correlationId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Failed to enqueue job message: JobId={JobId}, DocumentId={DocumentId}")]
    private partial void LogFailedToEnqueueJobMessage(Exception exception, Guid jobId, Guid documentId);
}
