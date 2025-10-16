using DocProcessing.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Pipeline;

/// <summary>
/// Activity to transition a job to Completed status.
/// </summary>
public sealed class CompleteJob
{
    private readonly IProcessJobService _jobService;
    private readonly ILogger<CompleteJob> _logger;

    public CompleteJob(
        IProcessJobService jobService,
        ILogger<CompleteJob> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(CompleteJob))]
    public async Task<bool> RunAsync(
        [ActivityTrigger] Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing job {JobId}", jobId);

        bool result = await _jobService.CompleteJobAsync(jobId, cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Failed to complete job {JobId}", jobId);
        }

        return result;
    }
}
