namespace DocProcessing.Domain.Validation;

using DocProcessing.Domain.Entities;

/// <summary>
/// Provides validation for ProcessJob status transitions according to the state machine rules.
/// </summary>
public static class ProcessJobStatusTransitions
{
    /// <summary>
    /// Determines if a transition from one status to another is valid.
    /// </summary>
    /// <param name="currentStatus">The current job status.</param>
    /// <param name="targetStatus">The desired target status.</param>
    /// <returns>True if the transition is valid; otherwise, false.</returns>
    public static bool IsValidTransition(ProcessJobStatus currentStatus, ProcessJobStatus targetStatus)
    {
        return (currentStatus, targetStatus) switch
        {
            (ProcessJobStatus.Pending, ProcessJobStatus.Processing) => true,
            (ProcessJobStatus.Processing, ProcessJobStatus.Completed) => true,
            (ProcessJobStatus.Processing, ProcessJobStatus.Failed) => true,
            (ProcessJobStatus.Failed, ProcessJobStatus.Pending) => true,
            (ProcessJobStatus.Processing, ProcessJobStatus.ManualReview) => true,
            (ProcessJobStatus.ManualReview, ProcessJobStatus.Processing) => true,
            (ProcessJobStatus.ManualReview, ProcessJobStatus.Completed) => true,
            (ProcessJobStatus.ManualReview, ProcessJobStatus.Failed) => true,

            // Same state (idempotent operations may allow this)
            _ when currentStatus == targetStatus => false,

            // All other transitions are invalid
            _ => false
        };
    }

    /// <summary>
    /// Gets all valid target statuses from the current status.
    /// </summary>
    /// <param name="currentStatus">The current job status.</param>
    /// <returns>A collection of valid target statuses.</returns>
    public static IReadOnlyCollection<ProcessJobStatus> GetValidTransitions(ProcessJobStatus currentStatus)
    {
        return currentStatus switch
        {
            ProcessJobStatus.Pending => [ProcessJobStatus.Processing],
            ProcessJobStatus.Processing => [ProcessJobStatus.Completed, ProcessJobStatus.Failed, ProcessJobStatus.ManualReview],
            ProcessJobStatus.Failed => [ProcessJobStatus.Pending],
            ProcessJobStatus.ManualReview => [ProcessJobStatus.Processing, ProcessJobStatus.Completed, ProcessJobStatus.Failed],
            ProcessJobStatus.Completed => [],
            _ => []
        };
    }
}
