using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Executors;

/// <summary>
/// Persistence stage activity.
/// Persists validated extracted data to the database.
/// </summary>
public sealed class PersistStageExecutor
{
    private readonly ILogger<PersistStageExecutor> _logger;
    private readonly IPipelineActivityFactory _pipelineActivityFactory;

    public PersistStageExecutor(
        ILogger<PersistStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory)
    {
        _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        _pipelineActivityFactory =
            pipelineActivityFactory ?? throw new ArgumentNullException(nameof(pipelineActivityFactory));
    }

    [Function(nameof(PersistStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Starting Persist stage for JobId: {JobId}, CorrelationId: {CorrelationId}",
            context.Job.JobId,
            context.CorrelationId);

        try
        {
            // TODO: Implement persistence logic
            // - Save extracted data to application database
            // - Update document metadata
            // - Store raw and processed data references
            // - Ensure transactional consistency

            await _pipelineActivityFactory
                .Create(ProcessJobStage.Persist)
                .ExecuteAsync(context, cancellationToken);

            _logger.LogInformation(
                "Persist stage completed successfully for JobId: {JobId}",
                context.Job.JobId);

            return StageResult.Success(
                output: new { DataPersisted = true },
                metadata: new Dictionary<string, object>
                {
                    ["CompletedAt"] = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Persist stage failed for JobId: {JobId}",
                context.Job.JobId);

            return StageResult.Failure(
                errorCode: "PERSIST_ERROR",
                errorMessage: ex.Message);
        }
    }
}
