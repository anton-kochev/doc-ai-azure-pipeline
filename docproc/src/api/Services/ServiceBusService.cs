using Api.Configuration;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.Services;

/// <summary>
/// Implementation of Service Bus message sending operations.
/// </summary>
public sealed class ServiceBusService : IServiceBusService, IAsyncDisposable
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

        // Create a Service Bus client using connection string or Managed Identity
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogInformation("Initializing Service Bus client with connection string");
            _client = new ServiceBusClient(_options.ConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(_options.Namespace))
        {
            _logger.LogInformation("Initializing Service Bus client with Managed Identity for namespace: {Namespace}", _options.Namespace);
            _client = new ServiceBusClient(_options.Namespace, new DefaultAzureCredential());
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
