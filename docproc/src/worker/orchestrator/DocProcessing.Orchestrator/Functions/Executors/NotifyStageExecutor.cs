using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Executors;

/// <summary>
/// Notification stage activity.
/// Sends notifications about processing completion and results.
/// </summary>
public sealed class NotifyStageExecutor
{
    private readonly ILogger<NotifyStageExecutor> _logger;
    private readonly IPipelineActivityFactory _pipelineActivityFactory;

    public NotifyStageExecutor(
        ILogger<NotifyStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory)
    {
        _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        _pipelineActivityFactory =
            pipelineActivityFactory ?? throw new ArgumentNullException(nameof(pipelineActivityFactory));
    }

    [Function(nameof(NotifyStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Starting Notify stage for JobId: {JobId}, CorrelationId: {CorrelationId}",
            context.Job.JobId,
            context.CorrelationId);

        var result = await _pipelineActivityFactory
            .Create(ProcessJobStage.Notify)
            .ExecuteAsync(context, cancellationToken);

        _logger.LogInformation(
            "Notify stage completed for JobId: {JobId}, Success: {IsSuccess}",
            context.Job.JobId,
            result.IsSuccess);

        return result;
    }
}
