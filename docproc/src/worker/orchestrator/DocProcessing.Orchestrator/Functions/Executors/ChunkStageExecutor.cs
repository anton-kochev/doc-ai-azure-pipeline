using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Executors;

/// <summary>
/// Chunking stage activity.
/// Splits normalized text into semantic chunks for downstream embedding.
/// </summary>
public sealed partial class ChunkStageExecutor
{
    private readonly ILogger<ChunkStageExecutor> _logger;
    private readonly IPipelineActivityFactory _pipelineActivityFactory;

    public ChunkStageExecutor(
        ILogger<ChunkStageExecutor> logger,
        IPipelineActivityFactory pipelineActivityFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(pipelineActivityFactory);

        _logger = logger;
        _pipelineActivityFactory = pipelineActivityFactory;
    }

    [Function(nameof(ChunkStageExecutor))]
    public async Task<StageResult> ExecuteAsync(
        [ActivityTrigger] StageContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogChunkStageStarting(_logger, context.Job.JobId, context.CorrelationId);

        StageResult result = await _pipelineActivityFactory
            .Create(ProcessJobStage.Chunk)
            .ExecuteAsync(context, cancellationToken);

        LogChunkStageCompleted(_logger, context.Job.JobId, result.IsSuccess);

        return result;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting Chunk stage for JobId: {JobId}, CorrelationId: {CorrelationId}")]
    private static partial void LogChunkStageStarting(
        ILogger logger,
        Guid jobId,
        string correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Chunk stage completed for JobId: {JobId}, Success: {IsSuccess}")]
    private static partial void LogChunkStageCompleted(
        ILogger logger,
        Guid jobId,
        bool isSuccess);
}
