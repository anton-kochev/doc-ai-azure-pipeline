using DocProcessing.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Pipeline;

/// <summary>
/// Activity to transition a job from ManualReview to Processing status.
/// </summary>
public sealed class ResumeFromManualReview
{
    private readonly IProcessJobService _jobService;
    private readonly ILogger<ResumeFromManualReview> _logger;

    public ResumeFromManualReview(
        IProcessJobService jobService,
        ILogger<ResumeFromManualReview> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(ResumeFromManualReview))]
    public async Task RunAsync(
        [ActivityTrigger] Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resuming job {JobId} from manual review", jobId);

        await _jobService.ResumeFromManualReviewAsync(jobId, cancellationToken);

        _logger.LogInformation("Successfully resumed job {JobId} from manual review", jobId);
    }
}
