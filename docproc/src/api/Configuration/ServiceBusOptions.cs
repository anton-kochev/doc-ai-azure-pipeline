namespace Api.Configuration;

/// <summary>
/// Configuration options for Azure Service Bus.
/// </summary>
public sealed class ServiceBusOptions
{
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
}
