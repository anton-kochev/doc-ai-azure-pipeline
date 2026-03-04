using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Executors;

/// <summary>
/// Preprocessing stage activity.
/// Cleans and prepares extracted text data for further processing.
/// </summary>
public sealed class PreprocessStageExecutor
{
    private readonly ILogger<PreprocessStageExecutor> _logger;
    private readonly IPipelineActivityFactory _pipelineActivityFactory;

    public PreprocessStageExecutor(
        ILogger<PreprocessStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory)
    {
        _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        _pipelineActivityFactory =
            pipelineActivityFactory ?? throw new ArgumentNullException(nameof(pipelineActivityFactory));
    }

    [Function(nameof(PreprocessStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Starting Preprocess stage for JobId: {JobId}, CorrelationId: {CorrelationId}",
            context.Job.JobId,
            context.CorrelationId);

        var result = await _pipelineActivityFactory
            .Create(ProcessJobStage.Preprocess)
            .ExecuteAsync(context, cancellationToken);

        _logger.LogInformation(
            "Preprocess stage completed for JobId: {JobId}, Success: {IsSuccess}",
            context.Job.JobId,
            result.IsSuccess);

        return result;
    }
}
