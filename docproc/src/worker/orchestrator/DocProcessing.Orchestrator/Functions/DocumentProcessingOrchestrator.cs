using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using DocProcessing.Application.Models;
using DocProcessing.Domain.Entities;
using DocProcessing.Orchestrator.Functions.Executors;
using DocProcessing.Orchestrator.Functions.Pipeline;

namespace DocProcessing.Orchestrator.Functions;

/// <summary>
/// Durable orchestrator for document processing workflow.
/// Executes all processing stages in sequence: OCR → Preprocess → Chunk → Embed → Extract → Validate → Persist → Notify.
/// </summary>
public sealed class DocumentProcessingOrchestrator
{
    /// <summary>
    /// Defines the sequence of processing stages to execute.
    /// </summary>
    private static readonly ProcessJobStage[] ProcessingStages =
    [
        ProcessJobStage.OCR,
        ProcessJobStage.Preprocess,
        ProcessJobStage.Chunk,
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
            // Accumulate stage output metadata across stages so each stage
            // can access outputs from previous stages (e.g., preprocessBlobPath)
            Dictionary<string, object> accumulatedMetadata = new()
            {
                [StageMetadataKeys.JobId] = jobId.ToString(),
                [StageMetadataKeys.DocumentId] = document.DocumentId.ToString(),
                [StageMetadataKeys.BlobContainer] = document.BlobContainer,
                [StageMetadataKeys.BlobPath] = document.BlobPath,
                [StageMetadataKeys.TenantId] = document.TenantId?.ToString() ?? string.Empty,
                [StageMetadataKeys.ExtractionProfile] = input.ExtractionProfile ?? string.Empty
            };

            foreach (ProcessJobStage stage in ProcessingStages)
            {
                logger.LogInformation(
                    "Executing stage {Stage} for JobId: {JobId}",
                    stage,
                    jobId);

                // Update stage in job representation
                job = job with { Stage = stage };

                StageContext stageContext = new(job, accumulatedMetadata, input.CorrelationId);

                // Call the appropriate stage activity
                string activityName = GetActivityNameForStage(stage);
                StageResult result = await context.CallActivityAsync<StageResult>(
                    activityName,
                    stageContext);

                // Merge stage output metadata into accumulated metadata for subsequent stages
                if (result.IsSuccess && result.Metadata is { Count: > 0 })
                {
                    foreach (KeyValuePair<string, object> kvp in result.Metadata)
                    {
                        accumulatedMetadata[kvp.Key] = kvp.Value;
                    }
                }

                // Check stage result
                if (!result.IsSuccess)
                {
                    logger.LogError(
                        "Stage {Stage} failed for JobId: {JobId}. ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                        stage,
                        jobId,
                        result.ErrorCode,
                        result.ErrorMessage);

                    // Check if manual review is required
                    if (result.ErrorCode == "MANUAL_REVIEW_REQUIRED")
                    {
                        logger.LogWarning(
                            "Manual review required for JobId: {JobId} at stage {Stage}. Reason: {ErrorMessage}",
                            jobId,
                            stage,
                            result.ErrorMessage);

                        // Request manual review (Processing → ManualReview)
                        await context.CallActivityAsync(
                            nameof(RequestManualReview),
                            new RequestManualReviewInput(jobId, result.ErrorMessage));

                        // Wait for external event with decision
                        logger.LogInformation(
                            "Waiting for manual review decision for JobId: {JobId}",
                            jobId);

                        string decision = await context.WaitForExternalEvent<string>("ManualReviewDecision");

                        logger.LogInformation(
                            "Received manual review decision '{Decision}' for JobId: {JobId}",
                            decision,
                            jobId);

                        // Handle decision (RESUME or REJECT only)
                        switch (decision?.ToUpperInvariant())
                        {
                            case "RESUME":
                                // Resume processing - continue to NEXT stage after manual review
                                logger.LogInformation(
                                    "Manual review resolved for JobId: {JobId}. Continuing to next stage after {Stage}.",
                                    jobId,
                                    stage);

                                await context.CallActivityAsync(nameof(ResumeFromManualReview), jobId);

                                logger.LogInformation(
                                    "Job {JobId} resumed from manual review. Continuing with next stage.",
                                    jobId);

                                // Continue with next stage in the loop
                                break;

                            case "REJECT":
                                // Manually rejected - fail the job
                                logger.LogWarning(
                                    "Manual review rejected for JobId: {JobId}. Failing job.",
                                    jobId);

                                await context.CallActivityAsync(
                                    nameof(RejectManualReview),
                                    new RejectManualReviewInput(
                                        jobId,
                                        "MANUAL_REVIEW_REJECTED",
                                        "Manually rejected during review"));

                                throw new InvalidOperationException(
                                    $"Job {jobId} manually rejected during review at stage {stage}");

                            default:
                                // Unknown decision - log error and fail
                                logger.LogError(
                                    "Unknown manual review decision '{Decision}' for JobId: {JobId}. Valid values: RESUME, REJECT.",
                                    decision,
                                    jobId);

                                await context.CallActivityAsync(
                                    nameof(FailJob),
                                    new FailJobRequest(
                                        jobId,
                                        "INVALID_MANUAL_REVIEW_DECISION",
                                        $"Invalid manual review decision: {decision}. Valid values: RESUME, REJECT."));

                                throw new InvalidOperationException(
                                    $"Invalid manual review decision '{decision}' for JobId {jobId}. Valid values: RESUME, REJECT.");
                        }
                    }
                    else
                    {
                        // Normal failure (not manual review) - fail the job
                        await context.CallActivityAsync(
                            nameof(FailJob),
                            new FailJobRequest(
                                jobId,
                                result.ErrorCode ?? "STAGE_FAILURE",
                                result.ErrorMessage ?? $"Stage {stage} failed"));

                        throw new InvalidOperationException(
                            $"Stage {stage} failed with error: {result.ErrorCode} - {result.ErrorMessage}");
                    }
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
            ProcessJobStage.Chunk => nameof(ChunkStageExecutor),
            ProcessJobStage.Embed => nameof(EmbedStageExecutor),
            ProcessJobStage.Extract => nameof(ExtractStageExecutor),
            ProcessJobStage.Validate => nameof(ValidateStageExecutor),
            ProcessJobStage.Persist => nameof(PersistStageExecutor),
            ProcessJobStage.Notify => nameof(NotifyStageExecutor),
            _ => throw new ArgumentException($"Unknown stage: {stage}", nameof(stage))
        };
    }
}
