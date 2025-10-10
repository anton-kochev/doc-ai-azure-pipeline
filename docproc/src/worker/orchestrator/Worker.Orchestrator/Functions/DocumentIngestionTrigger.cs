using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Worker.Orchestrator.Models;
using Worker.Orchestrator.Validation;

namespace Worker.Orchestrator.Functions;

/// <summary>
/// Azure Function that listens to Service Bus queue for document processing requests
/// and starts Durable Function orchestrations.
/// </summary>
public class DocumentIngestionTrigger
{
    private readonly ILogger<DocumentIngestionTrigger> _logger;

    public DocumentIngestionTrigger(ILogger<DocumentIngestionTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(DocumentIngestionTrigger))]
    public async Task Run(
        [ServiceBusTrigger("documents.process", Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext context)
    {
        string correlationId = message.CorrelationId ?? message.MessageId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = message.MessageId
        }))
        {
            _logger.LogInformation(
                "Received document processing message. MessageId: {MessageId}, CorrelationId: {CorrelationId}",
                message.MessageId,
                correlationId);

            ProcessDocumentMessage? payload = null;

            try
            {
                // Deserialize message body
                payload = JsonSerializer.Deserialize<ProcessDocumentMessage>(
                    message.Body.ToString(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                // Validate message
                (bool isValid, string? errorMessage) = MessageValidator.Validate(payload);

                if (!isValid)
                {
                    _logger.LogError(
                        "Message validation failed: {ErrorMessage}. Message will be dead-lettered.",
                        errorMessage);

                    await messageActions.DeadLetterMessageAsync(
                        message,
                        deadLetterReason: "ValidationFailed",
                        deadLetterErrorDescription: errorMessage);

                    return;
                }

                _logger.LogInformation(
                    "Message validated successfully. JobId: {JobId}, DocumentId: {DocumentId}, TenantId: {TenantId}",
                    payload!.JobId,
                    payload.DocumentId,
                    payload.TenantId);

                // Start durable orchestration
                string orchestrationInstanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                    nameof(DocumentProcessingOrchestrator),
                    payload);

                _logger.LogInformation(
                    "Started orchestration instance: {InstanceId} for JobId: {JobId}. IdempotencyKey: {IdempotencyKey}",
                    orchestrationInstanceId,
                    payload.JobId,
                    payload.IdempotencyKey);

                // Complete the message
                await messageActions.CompleteMessageAsync(message);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to deserialize message. Message will be dead-lettered.");

                await messageActions.DeadLetterMessageAsync(
                    message,
                    deadLetterReason: "DeserializationFailed",
                    deadLetterErrorDescription: ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error processing message for JobId: {JobId}. Message will be abandoned.",
                    payload?.JobId ?? "unknown");

                // Abandon message to retry
                await messageActions.AbandonMessageAsync(message);
            }
        }
    }
}
