using DocProcessing.Domain.Entities;

namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Exception thrown when attempting an invalid state transition for a ProcessJob.
/// </summary>
public sealed class InvalidStateTransitionException : Exception
{
    /// <summary>
    /// Gets the ID of the job for which the invalid transition was attempted.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// Gets the current status of the job.
    /// </summary>
    public ProcessJobStatus CurrentStatus { get; }

    /// <summary>
    /// Gets the status that was attempted to transition to.
    /// </summary>
    public ProcessJobStatus AttemptedStatus { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class.
    /// </summary>
    /// <param name="jobId">The ID of the job.</param>
    /// <param name="currentStatus">The current status of the job.</param>
    /// <param name="attemptedStatus">The status that was attempted to transition to.</param>
    public InvalidStateTransitionException(
        Guid jobId,
        ProcessJobStatus currentStatus,
        ProcessJobStatus attemptedStatus)
        : base($"Cannot transition job '{jobId}' from status '{currentStatus}' to '{attemptedStatus}'")
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class with a custom message.
    /// </summary>
    /// <param name="jobId">The ID of the job.</param>
    /// <param name="currentStatus">The current status of the job.</param>
    /// <param name="attemptedStatus">The status that was attempted to transition to.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public InvalidStateTransitionException(
        Guid jobId,
        ProcessJobStatus currentStatus,
        ProcessJobStatus attemptedStatus,
        string message)
        : base(message)
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="jobId">The ID of the job.</param>
    /// <param name="currentStatus">The current status of the job.</param>
    /// <param name="attemptedStatus">The status that was attempted to transition to.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidStateTransitionException(
        Guid jobId,
        ProcessJobStatus currentStatus,
        ProcessJobStatus attemptedStatus,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}
