namespace DocProcessing.Api.Services;

/// <summary>
/// Service for sending messages to Azure Service Bus.
/// </summary>
public interface IServiceBusService
{
    /// <summary>
    /// Enqueues a job message to the Service Bus queue.
    /// </summary>
    /// <param name="jobId">The job ID to enqueue.</param>
    /// <param name="documentId">The document ID associated with the job.</param>
    /// <param name="correlationId">The correlation ID for tracking.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnqueueJobAsync(Guid jobId, Guid documentId, string correlationId);
}
