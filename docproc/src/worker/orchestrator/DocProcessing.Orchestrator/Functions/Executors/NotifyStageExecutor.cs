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
    private readonly TimeProvider _timeProvider;

    public NotifyStageExecutor(
        ILogger<NotifyStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory,
        TimeProvider timeProvider)
    {
        _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        _pipelineActivityFactory =
            pipelineActivityFactory ?? throw new ArgumentNullException(nameof(pipelineActivityFactory));
        _timeProvider =
            timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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

        try
        {
            // TODO: Implement notification logic
            // - Send webhooks to registered endpoints
            // - Publish completion events to Service Bus/Event Grid
            // - Send email notifications (if configured)
            // - Update external systems via APIs

            await _pipelineActivityFactory
                .Create(ProcessJobStage.Notify)
                .ExecuteAsync(context, cancellationToken);

            _logger.LogInformation(
                "Notify stage completed successfully for JobId: {JobId}",
                context.Job.JobId);

            return StageResult.Success(
                output: new { NotificationsSent = true },
                metadata: new Dictionary<string, object>
                {
                    ["CompletedAt"] = _timeProvider.GetUtcNow()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Notify stage failed for JobId: {JobId}",
                context.Job.JobId);

            return StageResult.Failure(
                errorCode: "NOTIFY_ERROR",
                errorMessage: ex.Message);
        }
    }
}
