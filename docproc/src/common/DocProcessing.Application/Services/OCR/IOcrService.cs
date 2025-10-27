using DocProcessing.Application.Models.OCR;

namespace DocProcessing.Application.Services.OCR;

/// <summary>
/// Service for performing OCR (Optical Character Recognition) on documents.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Analyzes a document and extracts text, tables, and form fields.
    /// </summary>
    /// <param name="documentId">Document ID being analyzed.</param>
    /// <param name="jobId">Job ID for this OCR operation.</param>
    /// <param name="documentStream">Stream containing the document content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OCR results containing extracted text, tables, and metadata.</returns>
    Task<OcrResult> AnalyzeDocumentAsync(
        Guid documentId,
        Guid jobId,
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
