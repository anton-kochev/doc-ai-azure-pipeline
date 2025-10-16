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

        try
        {
            // TODO: Implement preprocessing logic
            // - Clean and normalize extracted text
            // - Remove noise, fix encoding issues
            // - Tokenize or chunk text as needed
            // - Apply extraction profile-specific transformations

            await _pipelineActivityFactory
                .Create(ProcessJobStage.Preprocess)
                .ExecuteAsync(context, cancellationToken);

            _logger.LogInformation(
                "Preprocess stage completed successfully for JobId: {JobId}",
                context.Job.JobId);

            return StageResult.Success(
                output: new { TextPreprocessed = true },
                metadata: new Dictionary<string, object>
                {
                    ["CompletedAt"] = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Preprocess stage failed for JobId: {JobId}",
                context.Job.JobId);

            return StageResult.Failure(
                errorCode: "PREPROCESS_ERROR",
                errorMessage: ex.Message);
        }
    }
}
