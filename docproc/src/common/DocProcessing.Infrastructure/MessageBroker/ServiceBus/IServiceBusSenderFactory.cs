using DocProcessing.Infrastructure.MessageBroker.ServiceBus;

namespace DocProcessing.Infrastructure.MessageBroker;

/// <summary>
/// Factory for creating Service Bus sender instances.
/// This enables dependency injection and testability.
/// </summary>
public interface IServiceBusSenderFactory
{
    /// <summary>
    /// Creates a Service Bus sender for the specified queue.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>A Service Bus sender instance.</returns>
    IServiceBusSender CreateSender(string queueName);
}
