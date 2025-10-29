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
    private readonly TimeProvider _timeProvider;

    public OcrStageExecutor(
        ILogger<OcrStageExecutor> logger,
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

    [Function(nameof(OcrStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Starting OCR stage. CorrelationId: {CorrelationId}, JobId: {JobId}",
            context.CorrelationId,
            context.Job.JobId);

        try
        {
            // TODO: Implement OCR logic
            // - Retrieve document from blob storage
            // - Call Azure Document Intelligence API
            // - Extract text, tables, and structure
            // - Store OCR results in metadata or temporary storage

            await _pipelineActivityFactory
                .Create(ProcessJobStage.OCR)
                .ExecuteAsync(context, cancellationToken);

            _logger.LogInformation(
                "OCR stage completed successfully. CorrelationId: {CorrelationId}, JobId: {JobId}",
                context.CorrelationId,
                context.Job.JobId);

            return StageResult.Success(
                output: new { TextExtracted = true },
                metadata: new Dictionary<string, object>
                {
                    ["CompletedAt"] = _timeProvider.GetUtcNow()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "OCR stage failed. CorrelationId: {CorrelationId}, JobId: {JobId}",
                context.CorrelationId,
                context.Job.JobId);

            return StageResult.Failure(
                errorCode: "OCR_ERROR",
                errorMessage: ex.Message);
        }
    }
}
