using Azure.Messaging.ServiceBus;

namespace DocProcessing.Infrastructure.MessageBroker.ServiceBus;

/// <summary>
/// Production implementation of IServiceBusSender that wraps Azure SDK's ServiceBusSender.
/// </summary>
internal sealed class ServiceBusSender : IServiceBusSender
{
    private readonly Azure.Messaging.ServiceBus.ServiceBusSender _sender;

    public ServiceBusSender(Azure.Messaging.ServiceBus.ServiceBusSender sender)
    {
        _sender = sender;
    }

    public async Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
    {
        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}
