namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Thrown when vector store retrieval fails due to infrastructure errors
/// (database connectivity, search service unavailable, embedding failure, etc.).
/// </summary>
public sealed class RetrievalFailedException : Exception
{
    /// <summary>
    /// Gets the document ID the retrieval was scoped to.
    /// </summary>
    public Guid DocumentId { get; }

    /// <summary>
    /// Gets the query text that was being searched.
    /// </summary>
    public string QueryText { get; }

    public RetrievalFailedException(Guid documentId, string queryText, string message)
        : base(message)
    {
        DocumentId = documentId;
        QueryText = queryText;
    }

    public RetrievalFailedException(
        Guid documentId, string queryText, string message, Exception? innerException)
        : base(message, innerException)
    {
        DocumentId = documentId;
        QueryText = queryText;
    }
}
