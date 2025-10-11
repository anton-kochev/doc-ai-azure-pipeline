using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Worker.Orchestrator.Models;

namespace Worker.Orchestrator.Functions;

/// <summary>
/// Durable orchestrator for document processing workflow.
/// </summary>
public class DocumentProcessingOrchestrator
{
    private readonly ILogger<DocumentProcessingOrchestrator> _logger;

    public DocumentProcessingOrchestrator(ILogger<DocumentProcessingOrchestrator> logger)
    {
        _logger = logger;
    }

    [Function(nameof(DocumentProcessingOrchestrator))]
    public Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ProcessDocumentMessage? input = context.GetInput<ProcessDocumentMessage>();

        if (input == null)
        {
            throw new ArgumentNullException(nameof(input), "Orchestrator input cannot be null");
        }

        ILogger logger = context.CreateReplaySafeLogger<DocumentProcessingOrchestrator>();

        logger.LogInformation(
            "Starting document processing orchestration for JobId: {JobId}, DocumentId: {DocumentId}",
            input.JobId,
            input.DocumentId);

        try
        {
            // TODO: Implement orchestration workflow
            // Example steps:
            // 1. Validate document exists in blob storage
            // 2. Call extraction activity (e.g., Azure Document Intelligence)
            // 3. Transform/enrich extracted data
            // 4. Store results
            // 5. Update job status

            logger.LogInformation(
                "Document processing orchestration completed for JobId: {JobId}",
                input.JobId);

            return Task.FromResult($"Completed processing for JobId: {input.JobId}");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Document processing orchestration failed for JobId: {JobId}",
                input.JobId);

            throw;
        }
    }
}
