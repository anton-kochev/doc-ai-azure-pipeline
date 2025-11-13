namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Thrown when a job with the specified ID cannot be found in the database.
/// </summary>
public sealed class JobNotFoundException : Exception
{
    /// <summary>
    /// Gets the ID of the job that was not found.
    /// </summary>
    public Guid JobId { get; }

    public JobNotFoundException(Guid jobId)
        : base($"Job with ID '{jobId}' was not found")
    {
        JobId = jobId;
    }

    public JobNotFoundException(Guid jobId, string message)
        : base(message)
    {
        JobId = jobId;
    }

    public JobNotFoundException(Guid jobId, string message, Exception? innerException)
        : base(message, innerException)
    {
        JobId = jobId;
    }
}
