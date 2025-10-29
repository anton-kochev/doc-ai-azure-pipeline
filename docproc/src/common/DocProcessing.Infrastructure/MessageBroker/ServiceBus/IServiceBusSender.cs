using Azure.Messaging.ServiceBus;

namespace DocProcessing.Infrastructure.MessageBroker.ServiceBus;

/// <summary>
/// Abstraction over Azure Service Bus sender operations.
/// This interface enables testability by wrapping the ServiceBusSender class.
/// </summary>
public interface IServiceBusSender : IAsyncDisposable
{
    /// <summary>
    /// Sends a message to the Service Bus queue or topic.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default);
}
