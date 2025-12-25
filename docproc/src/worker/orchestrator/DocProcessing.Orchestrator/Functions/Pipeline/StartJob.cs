using DocProcessing.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Pipeline;

/// <summary>
/// Activity to transition a job from Pending to Processing status.
/// </summary>
public sealed class StartJob
{
    private readonly IProcessJobService _jobService;
    private readonly ILogger<StartJob> _logger;

    public StartJob(
        IProcessJobService jobService,
        ILogger<StartJob> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(StartJob))]
    public async Task RunAsync(
        [ActivityTrigger] Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting job {JobId}", jobId);

        await _jobService.StartProcessingAsync(jobId, cancellationToken);

        _logger.LogInformation("Successfully started job {JobId}", jobId);
    }
}
