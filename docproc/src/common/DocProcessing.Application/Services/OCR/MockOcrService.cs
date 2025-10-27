using DocProcessing.Application.Configuration;
using DocProcessing.Application.Models.OCR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Application.Services.OCR;

/// <summary>
/// Mock OCR service for development and testing.
/// Returns realistic sample OCR results without calling external APIs.
/// </summary>
public sealed partial class MockOcrService : IOcrService
{
    private readonly ILogger<MockOcrService> _logger;
    private readonly OcrOptions _options;

    public MockOcrService(
        IOptions<OcrOptions> options,
        ILogger<MockOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OcrResult> AnalyzeDocumentAsync(
        Guid documentId,
        Guid jobId,
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        LogAnalyzingDocument(documentId, jobId);

        // Simulate processing delay
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        var startTime = DateTimeOffset.UtcNow;

        // Generate realistic mock data
        var pages = GenerateMockPages();
        var metadata = GenerateMockMetadata(pages, startTime);

        var result = new OcrResult(
            documentId,
            jobId,
            metadata,
            pages);

        LogOcrCompleted(
            documentId,
            metadata.PageCount,
            metadata.TotalTextBlocks,
            metadata.TotalTables,
            metadata.TotalFormFields);

        return result;
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Mock OCR service analyzing document {DocumentId} for job {JobId}")]
    private partial void LogAnalyzingDocument(Guid documentId, Guid jobId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Mock OCR completed for document {DocumentId}: {PageCount} pages, {TextBlocks} text blocks, {Tables} tables, {FormFields} form fields")]
    private partial void LogOcrCompleted(
        Guid documentId,
        int pageCount,
        int textBlocks,
        int tables,
        int formFields);

    private static List<OcrPage> GenerateMockPages()
    {
        return
        [
            // Page 1: Title page with header and body text
            new OcrPage(
                pageNumber: 1,
                width: 612,  // 8.5 inches * 72 points/inch
                height: 792, // 11 inches * 72 points/inch
                confidence: 0.96,
                textBlocks: [
                    new TextBlock(
                        "Invoice",
                        0.98,
                        1,
                        new BoundingBox(0.3, 0.1, 0.4, 0.05, 1),
                        "title",
                        "en"),
                    new TextBlock(
                        "ABC Corporation",
                        0.97,
                        1,
                        new BoundingBox(0.1, 0.18, 0.8, 0.04, 1),
                        "header",
                        "en"),
                    new TextBlock(
                        "123 Main Street, Suite 100\nNew York, NY 10001",
                        0.96,
                        1,
                        new BoundingBox(0.1, 0.23, 0.5, 0.06, 1),
                        "paragraph",
                        "en"),
                    new TextBlock(
                        "Date: January 15, 2024",
                        0.95,
                        1,
                        new BoundingBox(0.65, 0.23, 0.25, 0.03, 1),
                        "line",
                        "en"),
                    new TextBlock(
                        "Invoice Number: INV-2024-001",
                        0.94,
                        1,
                        new BoundingBox(0.65, 0.27, 0.25, 0.03, 1),
                        "line",
                        "en")
                ],
                formFields: [
                    new FormField("Invoice Number", "INV-2024-001", 0.98, 0.97, 1, fieldType: "text"),
                    new FormField("Date", "January 15, 2024", 0.97, 0.96, 1, fieldType: "date"),
                    new FormField("Company", "ABC Corporation", 0.96, 0.98, 1, fieldType: "text")
                ]),

            // Page 2: Table with line items
            new OcrPage(
                pageNumber: 2,
                width: 612,
                height: 792,
                confidence: 0.94,
                textBlocks: [
                    new TextBlock(
                        "Line Items",
                        0.95,
                        2,
                        new BoundingBox(0.1, 0.1, 0.3, 0.04, 2),
                        "header",
                        "en"),
                    new TextBlock(
                        "Please remit payment within 30 days.",
                        0.93,
                        2,
                        new BoundingBox(0.1, 0.75, 0.8, 0.03, 2),
                        "paragraph",
                        "en"),
                    new TextBlock(
                        "Thank you for your business!",
                        0.92,
                        2,
                        new BoundingBox(0.1, 0.8, 0.6, 0.03, 2),
                        "paragraph",
                        "en")
                ],
                tables: [
                    new TableData(
                        rowCount: 5,
                        columnCount: 4,
                        cells: [
                            // Header row
                            new TableCell(0, 0, "Item Description", 0.98, true),
                            new TableCell(0, 1, "Quantity", 0.97, true),
                            new TableCell(0, 2, "Unit Price", 0.96, true),
                            new TableCell(0, 3, "Total", 0.97, true),

                            // Data rows
                            new TableCell(1, 0, "Professional Services - Consulting", 0.95),
                            new TableCell(1, 1, "40", 0.96),
                            new TableCell(1, 2, "$150.00", 0.94),
                            new TableCell(1, 3, "$6,000.00", 0.95),

                            new TableCell(2, 0, "Software License - Enterprise", 0.94),
                            new TableCell(2, 1, "1", 0.97),
                            new TableCell(2, 2, "$2,500.00", 0.93),
                            new TableCell(2, 3, "$2,500.00", 0.94),

                            new TableCell(3, 0, "Training Session", 0.93),
                            new TableCell(3, 1, "8", 0.95),
                            new TableCell(3, 2, "$200.00", 0.92),
                            new TableCell(3, 3, "$1,600.00", 0.93),

                            // Total row
                            new TableCell(4, 0, "Total", 0.97, true, columnSpan: 3),
                            new TableCell(4, 3, "$10,100.00", 0.96, true)
                        ],
                        pageNumber: 2,
                        confidence: 0.94,
                        boundingBox: new BoundingBox(0.1, 0.15, 0.8, 0.55, 2))
                ],
                formFields: [
                    new FormField("Total Amount", "$10,100.00", 0.96, 0.95, 2, fieldType: "currency"),
                    new FormField("Payment Terms", "Net 30", 0.94, 0.93, 2, fieldType: "text")
                ])
        ];
    }

    private static OcrMetadata GenerateMockMetadata(
        IReadOnlyList<OcrPage> pages,
        DateTimeOffset startTime)
    {
        var endTime = DateTimeOffset.UtcNow;
        var duration = endTime - startTime;

        var totalTextBlocks = pages.Sum(p => p.TextBlocks.Count);
        var totalTables = pages.Sum(p => p.Tables.Count);
        var totalFormFields = pages.Sum(p => p.FormFields.Count);

        // Calculate average confidence across all pages
        var overallConfidence = pages.Count > 0
            ? pages.Average(p => p.Confidence)
            : 0.0;

        return new OcrMetadata(
            provider: "Mock",
            pageCount: pages.Count,
            processedAt: startTime,
            processingDuration: duration,
            overallConfidence: overallConfidence,
            totalTextBlocks: totalTextBlocks,
            totalTables: totalTables,
            totalFormFields: totalFormFields,
            primaryLanguage: "en",
            modelVersion: "mock-1.0",
            status: "Success",
            warnings: []);
    }
}
