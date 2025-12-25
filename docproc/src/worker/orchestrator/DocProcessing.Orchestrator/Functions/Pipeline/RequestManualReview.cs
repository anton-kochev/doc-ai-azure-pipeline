using DocProcessing.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Pipeline;

/// <summary>
/// Request to send a job to manual review.
/// </summary>
/// <param name="JobId">The job identifier.</param>
/// <param name="ReviewReason">Optional reason for manual review.</param>
public sealed record RequestManualReviewInput(Guid JobId, string? ReviewReason = null);

/// <summary>
/// Activity to transition a job from Processing to ManualReview status.
/// </summary>
public sealed class RequestManualReview
{
    private readonly IProcessJobService _jobService;
    private readonly ILogger<RequestManualReview> _logger;

    public RequestManualReview(
        IProcessJobService jobService,
        ILogger<RequestManualReview> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(RequestManualReview))]
    public async Task RunAsync(
        [ActivityTrigger] RequestManualReviewInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogInformation(
            "Requesting manual review for job {JobId}",
            input.JobId);

        await _jobService.RequestManualReviewAsync(
            input.JobId,
            input.ReviewReason,
            cancellationToken);

        _logger.LogInformation("Successfully requested manual review for job {JobId}", input.JobId);
    }
}
