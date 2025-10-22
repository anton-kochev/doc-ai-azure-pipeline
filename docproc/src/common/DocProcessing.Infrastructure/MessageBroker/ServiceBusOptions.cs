namespace DocProcessing.Infrastructure.MessageBroker;

/// <summary>
/// Configuration options for Azure Service Bus.
/// </summary>
public sealed class ServiceBusOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// The namespace for the Azure Service Bus (e.g., "myservicebus.servicebus.windows.net").
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// The name of the queue to send messages to.
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Connection string for Service Bus (alternative to using Managed Identity).
    /// If not provided, Managed Identity will be used.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Delay between retry attempts in seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Maximum retry delay in seconds (for exponential backoff).
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Timeout for Service Bus operations in seconds.
    /// </summary>
    public int TryTimeoutSeconds { get; set; } = 60;
}
