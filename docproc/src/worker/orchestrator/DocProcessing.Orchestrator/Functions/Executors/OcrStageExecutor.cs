using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Executors;

/// <summary>
/// OCR (Optical Character Recognition) stage activity.
/// Extracts text content from document images using Azure Document Intelligence or similar service.
/// </summary>
public sealed class OcrStageExecutor
{
    private readonly ILogger<OcrStageExecutor> _logger;
    private readonly IPipelineActivityFactory _pipelineActivityFactory;

    public OcrStageExecutor(
        ILogger<OcrStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory)
    {
        _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        _pipelineActivityFactory =
            pipelineActivityFactory ?? throw new ArgumentNullException(nameof(pipelineActivityFactory));
    }

    [Function(nameof(OcrStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Starting OCR stage for JobId: {JobId}, CorrelationId: {CorrelationId}",
            context.Job.JobId,
            context.CorrelationId);

        var result = await _pipelineActivityFactory
            .Create(ProcessJobStage.OCR)
            .ExecuteAsync(context, cancellationToken);

        _logger.LogInformation(
            "OCR stage completed for JobId: {JobId}, Success: {IsSuccess}",
            context.Job.JobId,
            result.IsSuccess);

        return result;
    }
}
