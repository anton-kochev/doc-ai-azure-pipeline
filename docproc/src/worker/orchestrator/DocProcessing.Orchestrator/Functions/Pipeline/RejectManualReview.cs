using DocProcessing.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions.Pipeline;

/// <summary>
/// Request to reject a job from manual review.
/// </summary>
/// <param name="JobId">The job identifier.</param>
/// <param name="ErrorCode">Optional error code for the rejection.</param>
/// <param name="ErrorMessage">Optional error message for the rejection.</param>
public sealed record RejectManualReviewInput(Guid JobId, string? ErrorCode = null, string? ErrorMessage = null);

/// <summary>
/// Activity to transition a job from ManualReview to Failed status.
/// </summary>
public sealed class RejectManualReview
{
    private readonly IProcessJobService _jobService;
    private readonly ILogger<RejectManualReview> _logger;

    public RejectManualReview(
        IProcessJobService jobService,
        ILogger<RejectManualReview> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(RejectManualReview))]
    public async Task RunAsync(
        [ActivityTrigger] RejectManualReviewInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogInformation(
            "Rejecting manual review for job {JobId}",
            input.JobId);

        await _jobService.RejectManualReviewAsync(
            input.JobId,
            input.ErrorCode,
            input.ErrorMessage,
            cancellationToken);

        _logger.LogInformation("Successfully rejected manual review for job {JobId}", input.JobId);
    }
}
