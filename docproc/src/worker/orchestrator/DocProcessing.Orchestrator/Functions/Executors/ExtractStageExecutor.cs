using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Executors;

/// <summary>
/// Information extraction stage activity.
/// Extracts structured information from documents based on extraction profile.
/// </summary>
public sealed class ExtractStageExecutor
{
    private readonly ILogger<ExtractStageExecutor> _logger;
    private readonly IPipelineActivityFactory _pipelineActivityFactory;

    public ExtractStageExecutor(
        ILogger<ExtractStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory)
    {
        _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        _pipelineActivityFactory =
            pipelineActivityFactory ?? throw new ArgumentNullException(nameof(pipelineActivityFactory));
    }

    [Function(nameof(ExtractStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Starting Extract stage for JobId: {JobId}, CorrelationId: {CorrelationId}",
            context.Job.JobId,
            context.CorrelationId);

        var result = await _pipelineActivityFactory
            .Create(ProcessJobStage.Extract)
            .ExecuteAsync(context, cancellationToken);

        _logger.LogInformation(
            "Extract stage completed for JobId: {JobId}, Success: {IsSuccess}",
            context.Job.JobId,
            result.IsSuccess);

        return result;
    }
}
