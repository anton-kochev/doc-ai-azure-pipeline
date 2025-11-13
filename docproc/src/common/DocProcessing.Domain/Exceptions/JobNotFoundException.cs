namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Exception thrown when a job with the specified ID cannot be found in the database.
/// </summary>
public sealed class JobNotFoundException : Exception
{
    /// <summary>
    /// Gets the ID of the job that was not found.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobNotFoundException"/> class.
    /// </summary>
    /// <param name="jobId">The ID of the job that was not found.</param>
    public JobNotFoundException(Guid jobId)
        : base($"Job with ID '{jobId}' was not found")
    {
        JobId = jobId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="jobId">The ID of the job that was not found.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public JobNotFoundException(Guid jobId, string message)
        : base(message)
    {
        JobId = jobId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobNotFoundException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="jobId">The ID of the job that was not found.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JobNotFoundException(Guid jobId, string message, Exception innerException)
        : base(message, innerException)
    {
        JobId = jobId;
    }
}
