using DocProcessing.Application.Models.OCR;

namespace DocProcessing.EndToEnd.Tests.Builders;

/// <summary>
/// Fluent builder for creating test OcrResult instances.
/// </summary>
public sealed class OcrResultBuilder
{
    private Guid _documentId = Guid.NewGuid();
    private Guid _jobId = Guid.NewGuid();
    private int _pageCount = 1;
    private double _confidence = 0.95;
    private string _provider = "Mock";
    private string _text = "Sample extracted text from the document.";
    private readonly List<OcrPage> _pages = [];

    public OcrResultBuilder WithDocumentId(Guid documentId) { _documentId = documentId; return this; }
    public OcrResultBuilder WithJobId(Guid jobId) { _jobId = jobId; return this; }
    public OcrResultBuilder WithPageCount(int count) { _pageCount = count; return this; }
    public OcrResultBuilder WithConfidence(double confidence) { _confidence = confidence; return this; }
    public OcrResultBuilder WithProvider(string provider) { _provider = provider; return this; }
    public OcrResultBuilder WithText(string text) { _text = text; return this; }
    public OcrResultBuilder WithPage(OcrPage page) { _pages.Add(page); return this; }

    public OcrResult Build()
    {
        List<OcrPage> pages = _pages.Count > 0
            ? _pages
            : Enumerable.Range(1, _pageCount).Select(i => CreateDefaultPage(i)).ToList();

        var metadata = new OcrMetadata(
            provider: _provider,
            pageCount: pages.Count,
            processedAt: DateTimeOffset.UtcNow,
            processingDuration: TimeSpan.FromMilliseconds(250),
            overallConfidence: _confidence,
            totalTextBlocks: pages.Sum(p => p.TextBlocks.Count),
            totalTables: pages.Sum(p => p.Tables.Count),
            totalFormFields: pages.Sum(p => p.FormFields.Count),
            primaryLanguage: "en",
            modelVersion: "2024-01-01",
            status: "Success");

        return new OcrResult(_documentId, _jobId, metadata, pages);
    }

    private OcrPage CreateDefaultPage(int pageNumber)
    {
        var textBlock = new TextBlock(
            text: _text,
            confidence: _confidence,
            pageNumber: pageNumber,
            blockType: "paragraph");

        return new OcrPage(
            pageNumber: pageNumber,
            width: 8.5,
            height: 11.0,
            confidence: _confidence,
            textBlocks: [textBlock]);
    }
}
