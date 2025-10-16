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
            throw new ArgumentException($"Invalid JobId format: {input.JobId}", nameof(input));
        }

        logger.LogInformation(
            "Starting document processing orchestration for JobId: {JobId}, DocumentId: {DocumentId}, CorrelationId: {CorrelationId}",
            input.JobId,
            input.DocumentId,
            input.CorrelationId);

        try
        {
            // Step 1: Retrieve and validate the job from the database
            ProcessJobModel? job = await context.CallActivityAsync<ProcessJobModel?>(nameof(GetJob), jobId);
            if (job == null)
            {
                throw new InvalidOperationException($"Job {jobId} not found in database");
            }

            // Step 2: Start processing (Pending → Processing)
            logger.LogInformation("Starting job {JobId}", jobId);
            bool started = await context.CallActivityAsync<bool>(
                nameof(StartJob),
                jobId);

            if (!started)
            {
                throw new InvalidOperationException($"Failed to start job {jobId}");
            }

            // Step 3: Execute all stages in sequence
            foreach (ProcessJobStage stage in ProcessingStages)
            {
                logger.LogInformation(
                    "Executing stage {Stage} for JobId: {JobId}",
                    stage,
                    jobId);

                // Update stage in job representation
                job = job with { Stage = stage };

                // Build stage context
                StageContext stageContext = new()
                {
                    Job = job,
                    CorrelationId = input.CorrelationId,
                    Metadata = new Dictionary<string, object>
                    {
                        ["JobId"] = jobId.ToString(),
                        ["DocumentId"] = input.DocumentId ?? string.Empty,
                        ["BlobContainer"] = input.BlobContainer ?? string.Empty,
                        ["BlobPath"] = input.BlobPath ?? string.Empty,
                        ["TenantId"] = input.TenantId ?? string.Empty,
                        ["ExtractionProfile"] = input.ExtractionProfile ?? string.Empty
                    }
                };

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
                    await context.CallActivityAsync<bool>(
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
            bool completed = await context.CallActivityAsync<bool>(
                nameof(CompleteJob),
                jobId);

            if (!completed)
            {
                throw new InvalidOperationException($"Failed to complete job {jobId}");
            }

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
                await context.CallActivityAsync<bool>(
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
