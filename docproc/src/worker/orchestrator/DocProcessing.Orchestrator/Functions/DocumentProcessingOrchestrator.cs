using DocProcessing.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using DocProcessing.Application.Models;
using DocProcessing.Domain.Entities;
using DocProcessing.Orchestrator.Functions.Executors;
using DocProcessing.Orchestrator.Pipeline;

namespace DocProcessing.Orchestrator.Functions;

/// <summary>
/// Durable orchestrator for document processing workflow.
/// Executes all processing stages in sequence: OCR → Preprocess → Embed → Extract → Validate → Persist → Notify.
/// </summary>
public class DocumentProcessingOrchestrator
{
    /// <summary>
    /// Defines the sequence of processing stages to execute.
    /// </summary>
    private static readonly ProcessJobStage[] ProcessingStages =
    [
        ProcessJobStage.OCR,
        ProcessJobStage.Preprocess,
        ProcessJobStage.Embed,
        ProcessJobStage.Extract,
        ProcessJobStage.Validate,
        ProcessJobStage.Persist,
        ProcessJobStage.Notify
    ];

    [Function(nameof(DocumentProcessingOrchestrator))]
    public async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ProcessDocumentMessage? input = context.GetInput<ProcessDocumentMessage>();

        _ = input ?? throw new ArgumentNullException(nameof(context), "Orchestrator input cannot be null");

        ILogger logger = context.CreateReplaySafeLogger<DocumentProcessingOrchestrator>();

        if (!Guid.TryParse(input.JobId, out Guid jobId))
        {
            throw new ArgumentException($"Invalid JobId format", nameof(context));
        }

        logger.LogInformation(
            "Starting document processing orchestration for JobId: {JobId}, CorrelationId: {CorrelationId}",
            input.JobId,
            input.CorrelationId);

        try
        {
            ProcessJobModel job =
                await context.CallActivityAsync<ProcessJobModel?>(nameof(GetJob), jobId) ??
                throw new InvalidOperationException($"Job {jobId} not found in database");

            Document document =
                await context.CallActivityAsync<Document?>(nameof(GetDocument), job.DocumentId) ??
                throw new InvalidOperationException($"Document {job.DocumentId} not found in database for Job {jobId}");
            
            logger.LogInformation(
                "Retrieved document {DocumentId} from database. BlobContainer: {BlobContainer}, BlobPath: {BlobPath}",
                document.DocumentId,
                document.BlobContainer,
                document.BlobPath);

            // Step 3: Start processing (Pending → Processing)
            logger.LogInformation("Starting job {JobId}", jobId);
            await context.CallActivityAsync(nameof(StartJob), jobId);

            // Step 4: Execute all stages in sequence
            foreach (ProcessJobStage stage in ProcessingStages)
            {
                logger.LogInformation(
                    "Executing stage {Stage} for JobId: {JobId}",
                    stage,
                    jobId);

                // Update stage in job representation
                job = job with { Stage = stage };

                // Build stage context using data from the database
                Dictionary<string, object> metadata = new()
                {
                    ["JobId"] = jobId.ToString(),
                    ["DocumentId"] = document.DocumentId.ToString(),
                    ["BlobContainer"] = document.BlobContainer,
                    ["BlobPath"] = document.BlobPath,
                    ["TenantId"] = document.TenantId?.ToString() ?? string.Empty,
                    ["ExtractionProfile"] = input.ExtractionProfile ?? string.Empty
                };

                StageContext stageContext = new(job, metadata, input.CorrelationId);

                // Call the appropriate stage activity
                string activityName = GetActivityNameForStage(stage);
                StageResult result = await context.CallActivityAsync<StageResult>(
                    activityName,
                    stageContext);

                // Check stage result
                if (!result.IsSuccess)
                {
                    logger.LogError(
                        "Stage {Stage} failed for JobId: {JobId}. ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                        stage,
                        jobId,
                        result.ErrorCode,
                        result.ErrorMessage);

                    // Fail the job
                    await context.CallActivityAsync(
                        nameof(FailJob),
                        new FailJobRequest(
                            jobId,
                            result.ErrorCode ?? "STAGE_FAILURE",
                            result.ErrorMessage ?? $"Stage {stage} failed"));

                    throw new InvalidOperationException(
                        $"Stage {stage} failed with error: {result.ErrorCode} - {result.ErrorMessage}");
                }

                logger.LogInformation(
                    "Stage {Stage} completed successfully for JobId: {JobId}",
                    stage,
                    jobId);
            }

            // Step 4: Complete the job (Processing → Completed)
            logger.LogInformation("Completing job {JobId}", jobId);
            await context.CallActivityAsync(nameof(CompleteJob), jobId);

            logger.LogInformation(
                "Document processing orchestration completed successfully for JobId: {JobId}",
                input.JobId);

            return $"Completed processing for JobId: {input.JobId}";
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Document processing orchestration failed for JobId: {JobId}",
                input.JobId);

            // Attempt to mark the job as failed if not already done
            try
            {
                await context.CallActivityAsync(
                    nameof(FailJob),
                    new FailJobRequest(
                        jobId,
                        "ORCHESTRATION_ERROR",
                        ex.Message));
            }
            catch (Exception failEx)
            {
                logger.LogError(
                    failEx,
                    "Failed to mark job {JobId} as failed",
                    jobId);
            }

            throw;
        }
    }

    /// <summary>
    /// Maps a ProcessJobStage to its corresponding activity name.
    /// </summary>
    private static string GetActivityNameForStage(ProcessJobStage stage)
    {
        return stage switch
        {
            ProcessJobStage.OCR => nameof(OcrStageExecutor),
            ProcessJobStage.Preprocess => nameof(PreprocessStageExecutor),
            ProcessJobStage.Embed => nameof(EmbedStageExecutor),
            ProcessJobStage.Extract => nameof(ExtractStageExecutor),
            ProcessJobStage.Validate => nameof(ValidateStageExecutor),
            ProcessJobStage.Persist => nameof(PersistStageExecutor),
            ProcessJobStage.Notify => nameof(NotifyStageExecutor),
            _ => throw new ArgumentException($"Unknown stage: {stage}", nameof(stage))
        };
    }
}
