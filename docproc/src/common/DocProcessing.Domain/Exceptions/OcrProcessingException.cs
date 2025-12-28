namespace DocProcessing.Domain.Exceptions;

/// <summary>
/// Thrown when OCR (Optical Character Recognition) processing fails.
/// This exception is thrown by OCR service implementations when document analysis
/// encounters errors such as API failures, invalid documents, or processing timeouts.
/// </summary>
public sealed class OcrProcessingException : Exception
{
    /// <summary>
    /// Gets the ID of the document that failed OCR processing.
    /// </summary>
    public Guid DocumentId { get; }

    /// <summary>
    /// Gets the ID of the job that was processing the document.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// Gets the HTTP status code if the failure was due to an API error.
    /// </summary>
    public int? StatusCode { get; }

    public OcrProcessingException(Guid documentId, Guid jobId, string message)
        : base(message)
    {
        DocumentId = documentId;
        JobId = jobId;
    }

    public OcrProcessingException(Guid documentId, Guid jobId, string message, Exception? innerException)
        : base(message, innerException)
    {
        DocumentId = documentId;
        JobId = jobId;
    }

    public OcrProcessingException(Guid documentId, Guid jobId, string message, int statusCode)
        : base(message)
    {
        DocumentId = documentId;
        JobId = jobId;
        StatusCode = statusCode;
    }

    public OcrProcessingException(Guid documentId, Guid jobId, string message, int statusCode, Exception? innerException)
        : base(message, innerException)
    {
        DocumentId = documentId;
        JobId = jobId;
        StatusCode = statusCode;
    }
}
