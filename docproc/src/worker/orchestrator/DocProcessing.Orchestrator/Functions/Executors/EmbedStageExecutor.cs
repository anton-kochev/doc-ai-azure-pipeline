using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Executors;

/// <summary>
/// Embedding stage activity.
/// Generates vector embeddings from preprocessed text for semantic search and analysis.
/// </summary>
public sealed class EmbedStageExecutor
{
    private readonly ILogger<EmbedStageExecutor> _logger;
    private readonly IPipelineActivityFactory _pipelineActivityFactory;

    public EmbedStageExecutor(
        ILogger<EmbedStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory)
    {
        _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        _pipelineActivityFactory =
            pipelineActivityFactory ?? throw new ArgumentNullException(nameof(pipelineActivityFactory));
    }

    [Function(nameof(EmbedStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Starting Embed stage for JobId: {JobId}, CorrelationId: {CorrelationId}",
            context.Job.JobId,
            context.CorrelationId);

        var result = await _pipelineActivityFactory
            .Create(ProcessJobStage.Embed)
            .ExecuteAsync(context, cancellationToken);

        _logger.LogInformation(
            "Embed stage completed for JobId: {JobId}, Success: {IsSuccess}",
            context.Job.JobId,
            result.IsSuccess);

        return result;
    }
}
