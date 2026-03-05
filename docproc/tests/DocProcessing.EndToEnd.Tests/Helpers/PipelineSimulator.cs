using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models;
using DocProcessing.Application.Pipeline;
using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace DocProcessing.EndToEnd.Tests.Helpers;

/// <summary>
/// Simulates the orchestrator's stage loop in-process.
///
/// The orchestrator now accumulates stage output metadata across stages (fixed in this changeset),
/// matching this simulator's forwarding behavior.
/// </summary>
public sealed class PipelineSimulator
{
    /// <summary>
    /// The ordered stage sequence — must match DocumentProcessingOrchestrator.ProcessingStages.
    /// </summary>
    public static readonly ProcessJobStage[] Stages =
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

    private readonly IProcessJobService _jobService;
    private readonly IPipelineActivityFactory _activityFactory;
    private readonly IApplicationDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider;

    public PipelineSimulator(
        IProcessJobService jobService,
        IPipelineActivityFactory activityFactory,
        IApplicationDbContext dbContext,
        FakeTimeProvider timeProvider)
    {
        _jobService = jobService;
        _activityFactory = activityFactory;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Runs the full pipeline for a job.
    /// </summary>
    /// <param name="jobId">The job to process.</param>
    /// <param name="startFromStageIndex">Optional stage index to resume from (for manual review resume).</param>
    /// <param name="forwardedMetadata">Metadata forwarded from previous stages (for resume).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the pipeline execution.</returns>
    public async Task<PipelineResult> RunAsync(
        Guid jobId,
        int startFromStageIndex = 0,
        Dictionary<string, object>? forwardedMetadata = null,
        CancellationToken cancellationToken = default)
    {
        // Start processing (Pending → Processing) — only if starting from beginning
        if (startFromStageIndex == 0)
        {
            await _jobService.StartProcessingAsync(jobId, cancellationToken);
        }

        // Get job and document info
        ProcessJob job = await _dbContext.ProcessJobs
            .AsNoTracking()
            .FirstAsync(j => j.JobId == jobId, cancellationToken);

        Document? document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == job.DocumentId, cancellationToken);

        // Accumulated metadata from stage outputs (forwarded between stages)
        var accumulatedMetadata = forwardedMetadata ?? new Dictionary<string, object>();

        for (int i = startFromStageIndex; i < Stages.Length; i++)
        {
            ProcessJobStage stage = Stages[i];
            IJobStageActivity activity = _activityFactory.Create(stage);

            // Build metadata: start from document record (matching orchestrator),
            // then merge accumulated stage output metadata (DIVERGENCE: intended fix)
            var metadata = new Dictionary<string, object>();

            if (document is not null)
            {
                metadata[StageMetadataKeys.BlobContainer] = document.BlobContainer;
                metadata[StageMetadataKeys.BlobPath] = document.BlobPath;
                metadata[StageMetadataKeys.TenantId] = document.TenantId?.ToString() ?? "default";
            }

            // Merge forwarded metadata from previous stages
            foreach (var kvp in accumulatedMetadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }

            var jobModel = new ProcessJobModel(
                job.JobId,
                job.DocumentId,
                job.IdempotencyKey,
                ProcessJobStatus.Processing,
                stage);

            var context = new StageContext(jobModel, metadata, job.CorrelationId);

            StageResult result = await activity.ExecuteAsync(context, cancellationToken);

            // Advance time between stages
            _timeProvider.Advance(TimeSpan.FromSeconds(1));

            if (!result.IsSuccess)
            {
                if (result.ErrorCode == "MANUAL_REVIEW_REQUIRED")
                {
                    return new PipelineResult
                    {
                        IsSuccess = false,
                        RequiresManualReview = true,
                        FailedStage = stage,
                        FailedStageIndex = i,
                        ErrorCode = result.ErrorCode,
                        ErrorMessage = result.ErrorMessage,
                        AccumulatedMetadata = accumulatedMetadata
                    };
                }

                return new PipelineResult
                {
                    IsSuccess = false,
                    FailedStage = stage,
                    FailedStageIndex = i,
                    ErrorCode = result.ErrorCode,
                    ErrorMessage = result.ErrorMessage,
                    AccumulatedMetadata = accumulatedMetadata
                };
            }

            // Accumulate output metadata for next stages.
            // Some activities (e.g. OcrStageActivity) put data in Output as a Dictionary,
            // while others (e.g. PreprocessStageActivity) use the Metadata property.
            // We merge both to forward all stage output data.
            if (result.Output is { Count: > 0 })
            {
                foreach (var kvp in result.Output)
                {
                    accumulatedMetadata[kvp.Key] = kvp.Value;
                }
            }

            foreach (var kvp in result.Metadata)
            {
                accumulatedMetadata[kvp.Key] = kvp.Value;
            }
        }

        // All stages succeeded
        await _jobService.CompleteJobAsync(jobId, cancellationToken);

        return new PipelineResult
        {
            IsSuccess = true,
            AccumulatedMetadata = accumulatedMetadata
        };
    }
}

public sealed class PipelineResult
{
    public required bool IsSuccess { get; init; }
    public bool RequiresManualReview { get; init; }
    public ProcessJobStage? FailedStage { get; init; }
    public int? FailedStageIndex { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> AccumulatedMetadata { get; init; } = new();
}
