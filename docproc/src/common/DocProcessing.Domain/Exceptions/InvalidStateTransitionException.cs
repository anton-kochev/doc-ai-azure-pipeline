using DocProcessing.Domain.Entities;

namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Thrown when attempting an invalid state transition for a ProcessJob.
/// </summary>
public sealed class InvalidStateTransitionException : Exception
{
    public Guid JobId { get; }
    public ProcessJobStatus CurrentStatus { get; }
    public ProcessJobStatus AttemptedStatus { get; }

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

    public InvalidStateTransitionException(
        Guid jobId,
        ProcessJobStatus currentStatus,
        ProcessJobStatus attemptedStatus,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}
