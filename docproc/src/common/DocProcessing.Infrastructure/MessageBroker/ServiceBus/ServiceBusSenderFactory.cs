using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Infrastructure.MessageBroker.ServiceBus;

/// <summary>
/// Factory implementation for creating Service Bus senders.
/// Handles both connection string and Managed Identity authentication.
/// </summary>
public sealed partial class ServiceBusSenderFactory : IServiceBusSenderFactory, IAsyncDisposable
{
    private readonly ILogger<ServiceBusSenderFactory> _logger;
    private readonly ServiceBusOptions _options;
    private readonly ServiceBusClient _client;

    public ServiceBusSenderFactory(
        ILogger<ServiceBusSenderFactory> logger,
        IOptions<ServiceBusOptions> options)
    {
        _logger = logger;
        _options = options.Value;

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
    }

    public IServiceBusSender CreateSender(string queueName)
    {
        Azure.Messaging.ServiceBus.ServiceBusSender sender = _client.CreateSender(queueName);
        LogServiceBusSenderCreated(queueName);
        return new ServiceBusSender(sender);
    }

    public async ValueTask DisposeAsync()
    {
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
}
