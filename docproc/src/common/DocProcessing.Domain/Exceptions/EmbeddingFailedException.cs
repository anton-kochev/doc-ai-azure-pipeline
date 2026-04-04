namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Thrown when embedding generation fails due to API errors, timeouts,
/// or other infrastructure issues in the embedding service.
/// </summary>
public sealed class EmbeddingFailedException : Exception
{
    /// <summary>
    /// Gets the ID of the document whose chunks failed to embed.
    /// </summary>
    public Guid DocumentId { get; }

    /// <summary>
    /// Gets the ID of the job that was processing the document.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// Gets the number of chunks that were being embedded when the failure occurred.
    /// </summary>
    public int ChunkCount { get; }

    public EmbeddingFailedException(int chunkCount, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ChunkCount = chunkCount;
    }

    public EmbeddingFailedException(Guid documentId, Guid jobId, int chunkCount, string message)
        : base(message)
    {
        DocumentId = documentId;
        JobId = jobId;
        ChunkCount = chunkCount;
    }

    public EmbeddingFailedException(Guid documentId, Guid jobId, int chunkCount, string message, Exception? innerException)
        : base(message, innerException)
    {
        DocumentId = documentId;
        JobId = jobId;
        ChunkCount = chunkCount;
    }
}
