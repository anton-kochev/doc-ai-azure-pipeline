using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DocProcessing.Application.Validation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using DocProcessing.Application.Models;

namespace DocProcessing.Orchestrator.Functions;

/// <summary>
/// Azure Function that listens to Service Bus queue for document processing requests
/// and starts Durable Function orchestrations.
/// </summary>
public partial class DocumentIngestionTrigger
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
    
    private readonly ILogger<DocumentIngestionTrigger> _logger;

    public DocumentIngestionTrigger(ILogger<DocumentIngestionTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(DocumentIngestionTrigger))]
    public async Task Run(
        [ServiceBusTrigger("documents.process", Connection = "ServiceBusConnection")]
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
            LogReceivedDocumentProcessingMessage(correlationId, message.MessageId);

            ProcessDocumentMessage? payload = null;

            try
            {
                // Deserialize message body
                payload = JsonSerializer
                    .Deserialize<ProcessDocumentMessage>(message.Body.ToString(), JsonSerializerOptions);

                // Validate message
                (bool isValid, string? errorMessage) = MessageValidator.Validate(payload);

                if (!isValid)
                {
                    LogMessageValidationFailed(errorMessage);

                    await messageActions.DeadLetterMessageAsync(
                        message,
                        deadLetterReason: "ValidationFailed",
                        deadLetterErrorDescription: errorMessage);

                    return;
                }

                LogMessageValidatedSuccessfully(correlationId, payload!.JobId, payload.DocumentId, payload.TenantId);

                // Start durable orchestration
                string orchestrationInstanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                    nameof(DocumentProcessingOrchestrator),
                    payload);

                LogStartedOrchestrationInstance(correlationId, orchestrationInstanceId, payload.JobId, payload.IdempotencyKey);

                // Complete the message
                await messageActions.CompleteMessageAsync(message);
            }
            catch (JsonException ex)
            {
                LogFailedToDeserializeMessage(ex);

                await messageActions.DeadLetterMessageAsync(
                    message,
                    deadLetterReason: "DeserializationFailed",
                    deadLetterErrorDescription: ex.Message);
            }
            catch (Exception ex)
            {
                LogUnexpectedErrorProcessingMessage(ex, correlationId, payload?.JobId ?? "unknown");

                // Abandon message to retry
                await messageActions.AbandonMessageAsync(message);
            }
        }
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Received document processing message. CorrelationId: {CorrelationId}, MessageId: {MessageId}")]
    private partial void LogReceivedDocumentProcessingMessage(string correlationId, string messageId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Message validation failed: {ErrorMessage}. Message will be dead-lettered.")]
    private partial void LogMessageValidationFailed(string? errorMessage);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Message validated successfully. CorrelationId: {CorrelationId}, JobId: {JobId}, DocumentId: {DocumentId}, TenantId: {TenantId}")]
    private partial void LogMessageValidatedSuccessfully(string correlationId, string jobId, string? documentId, string? tenantId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Started orchestration instance. CorrelationId: {CorrelationId}, InstanceId: {InstanceId}, JobId: {JobId}, IdempotencyKey: {IdempotencyKey}")]
    private partial void LogStartedOrchestrationInstance(string correlationId, string instanceId, string jobId, string? idempotencyKey);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Failed to deserialize message. Message will be dead-lettered.")]
    private partial void LogFailedToDeserializeMessage(Exception exception);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Unexpected error processing message. CorrelationId: {CorrelationId}, JobId: {JobId}. Message will be abandoned.")]
    private partial void LogUnexpectedErrorProcessingMessage(Exception exception, string correlationId, string jobId);
}
