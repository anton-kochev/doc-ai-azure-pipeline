using DocProcessing.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Pipeline;

/// <summary>
/// Request to fail a job with error details.
/// </summary>
/// <param name="JobId">The job identifier.</param>
/// <param name="ErrorCode">The error code.</param>
/// <param name="ErrorMessage">The error message.</param>
public sealed record FailJobRequest(Guid JobId, string ErrorCode, string ErrorMessage);

/// <summary>
/// Activity to transition a job to Failed status with error details.
/// </summary>
public sealed class FailJob
{
    private readonly IProcessJobService _jobService;
    private readonly ILogger<FailJob> _logger;

    public FailJob(
        IProcessJobService jobService,
        ILogger<FailJob> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(FailJob))]
    public async Task<bool> RunAsync(
        [ActivityTrigger] FailJobRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Failing job {JobId} with error code {ErrorCode}",
            request.JobId,
            request.ErrorCode);

        bool result = await _jobService.FailJobAsync(
            request.JobId,
            request.ErrorCode,
            request.ErrorMessage,
            cancellationToken);

        if (!result)
        {
            _logger.LogWarning("Failed to update job {JobId} to Failed status", request.JobId);
        }

        return result;
    }
}
