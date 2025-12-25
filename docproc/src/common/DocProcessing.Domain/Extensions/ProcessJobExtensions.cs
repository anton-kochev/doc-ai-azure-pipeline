namespace DocProcessing.Domain.Extensions;

using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Exceptions;
using DocProcessing.Domain.Validation;

/// <summary>
/// Extension methods for ProcessJob entity.
/// </summary>
public static class ProcessJobExtensions
{
    /// <summary>
    /// Validates that the job can transition to the target status and throws if invalid.
    /// </summary>
    /// <param name="job">The process job.</param>
    /// <param name="targetStatus">The desired target status.</param>
    /// <exception cref="InvalidStateTransitionException">
    /// Thrown when the transition from current status to target status is not valid.
    /// </exception>
    public static void ThrowIfInvalidTransitionTo(this ProcessJob job, ProcessJobStatus targetStatus)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!ProcessJobStatusTransitions.IsValidTransition(job.Status, targetStatus))
        {
            throw new InvalidStateTransitionException(job.JobId, job.Status, targetStatus);
        }
    }
}
