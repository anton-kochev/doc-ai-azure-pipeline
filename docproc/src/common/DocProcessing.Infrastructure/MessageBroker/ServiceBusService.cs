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
public sealed class ServiceBusService : IMessagingService, IAsyncDisposable
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

        _logger.LogInformation(
            "Configured Service Bus retry policy: MaxRetries={MaxRetries}, Delay={Delay}s, MaxDelay={MaxDelay}s, TryTimeout={TryTimeout}s",
            _options.MaxRetries,
            _options.RetryDelaySeconds,
            _options.MaxRetryDelaySeconds,
            _options.TryTimeoutSeconds);

        // Create a Service Bus client using connection string or Managed Identity
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogInformation("Initializing Service Bus client with connection string");
            _client = new ServiceBusClient(_options.ConnectionString, clientOptions);
        }
        else if (!string.IsNullOrWhiteSpace(_options.Namespace))
        {
            _logger.LogInformation("Initializing Service Bus client with Managed Identity for namespace: {Namespace}", _options.Namespace);
            _client = new ServiceBusClient(_options.Namespace, new DefaultAzureCredential(), clientOptions);
        }
        else
        {
            throw new InvalidOperationException("Either ServiceBus:ConnectionString or ServiceBus:Namespace must be configured");
        }

        _sender = _client.CreateSender(_options.QueueName);
        _logger.LogInformation("Service Bus sender created for queue: {QueueName}", _options.QueueName);
    }

    /// <inheritdoc />
    public async Task EnqueueJobAsync(Guid jobId, Guid documentId, string correlationId)
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
            await _sender.SendMessageAsync(message);

            _logger.LogInformation(
                "Successfully enqueued job message: JobId={JobId}, DocumentId={DocumentId}, CorrelationId={CorrelationId}",
                jobId,
                documentId,
                correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to enqueue job message: JobId={JobId}, DocumentId={DocumentId}",
                jobId,
                documentId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
