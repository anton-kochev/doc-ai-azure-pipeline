namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Service for managing ProcessJob entities and job orchestration.
/// </summary>
public interface IProcessJobService
{
    /// <summary>
    /// Gets an existing non-terminal job by idempotency key, or creates a new job if none exists.
    /// </summary>
    /// <param name="documentId">The document ID associated with the job.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <param name="sha256Hash">The SHA256 hash of the document.</param>
    /// <param name="extractionProfile">Optional extraction profile name.</param>
    /// <param name="correlationId">Optional correlation ID for tracking.</param>
    /// <param name="priority">Job priority (0-255, default 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the job ID and a boolean indicating whether it was newly created (true) or already existed (false).</returns>
    Task<(Guid JobId, bool IsNew)> GetOrCreateJobAsync(
        Guid documentId,
        Guid? tenantId,
        byte[] sha256Hash,
        string? extractionProfile = null,
        string? correlationId = null,
        byte priority = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a failed job back to Pending status for retry.
    /// </summary>
    /// <param name="jobId">The ID of the job to retry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The correlation ID of the retried job.</returns>
    /// <exception cref="DocProcessing.Domain.Exceptions.JobNotFoundException">Thrown when the job with the specified ID does not exist.</exception>
    /// <exception cref="DocProcessing.Domain.Exceptions.InvalidStateTransitionException">Thrown when the job is not in Failed status.</exception>
    Task<string> RetryFailedJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Computes the idempotency key for a job based on tenant, document hash, and extraction profile.
    /// </summary>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <param name="sha256Hash">The SHA256 hash of the document.</param>
    /// <param name="extractionProfile">Optional extraction profile name.</param>
    /// <returns>A string representing the idempotency key.</returns>
    string ComputeIdempotencyKey(Guid? tenantId, byte[] sha256Hash, string? extractionProfile);

    /// <summary>
    /// Transitions a job from Pending to Processing status.
    /// </summary>
    /// <param name="jobId">The ID of the job to start processing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DocProcessing.Domain.Exceptions.JobNotFoundException">Thrown when the job with the specified ID does not exist.</exception>
    /// <exception cref="DocProcessing.Domain.Exceptions.InvalidStateTransitionException">Thrown when the job is not in Pending status.</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to acquire the job due to concurrency conflicts after retries.</exception>
    Task StartProcessingAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a job from Processing to Completed status.
    /// </summary>
    /// <param name="jobId">The ID of the job to mark as completed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DocProcessing.Domain.Exceptions.JobNotFoundException">Thrown when the job with the specified ID does not exist.</exception>
    /// <exception cref="DocProcessing.Domain.Exceptions.InvalidStateTransitionException">Thrown when the job is not in Processing status.</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to update the job due to concurrency conflicts after retries.</exception>
    Task CompleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a job from Processing to Failed status.
    /// </summary>
    /// <param name="jobId">The ID of the job to mark as failed.</param>
    /// <param name="errorCode">Optional error code describing the failure.</param>
    /// <param name="errorMessage">Optional error message describing the failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DocProcessing.Domain.Exceptions.JobNotFoundException">Thrown when the job with the specified ID does not exist.</exception>
    /// <exception cref="DocProcessing.Domain.Exceptions.InvalidStateTransitionException">Thrown when the job is not in Processing status.</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to update the job due to concurrency conflicts after retries.</exception>
    Task FailJobAsync(Guid jobId, string? errorCode = null, string? errorMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a job from Processing to ManualReview status.
    /// </summary>
    /// <param name="jobId">The ID of the job to mark for manual review.</param>
    /// <param name="reviewReason">Optional reason describing why manual review is needed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DocProcessing.Domain.Exceptions.JobNotFoundException">Thrown when the job with the specified ID does not exist.</exception>
    /// <exception cref="DocProcessing.Domain.Exceptions.InvalidStateTransitionException">Thrown when the job is not in Processing status.</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to update the job due to concurrency conflicts after retries.</exception>
    Task RequestManualReviewAsync(Guid jobId, string? reviewReason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a job from ManualReview to Processing status to reprocess after manual intervention.
    /// </summary>
    /// <param name="jobId">The ID of the job to resume processing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DocProcessing.Domain.Exceptions.JobNotFoundException">Thrown when the job with the specified ID does not exist.</exception>
    /// <exception cref="DocProcessing.Domain.Exceptions.InvalidStateTransitionException">Thrown when the job is not in ManualReview status.</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to update the job due to concurrency conflicts after retries.</exception>
    Task ResumeFromManualReviewAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a job from ManualReview to Failed status after manual rejection.
    /// </summary>
    /// <param name="jobId">The ID of the job to mark as manually rejected.</param>
    /// <param name="errorCode">Optional error code describing the rejection reason.</param>
    /// <param name="errorMessage">Optional error message describing the rejection reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DocProcessing.Domain.Exceptions.JobNotFoundException">Thrown when the job with the specified ID does not exist.</exception>
    /// <exception cref="DocProcessing.Domain.Exceptions.InvalidStateTransitionException">Thrown when the job is not in ManualReview status.</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to update the job due to concurrency conflicts after retries.</exception>
    Task RejectManualReviewAsync(Guid jobId, string? errorCode = null, string? errorMessage = null, CancellationToken cancellationToken = default);
}
