using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Pipeline;
using DocProcessing.Application.Services;
using DocProcessing.Application.Services.OCR;
using DocProcessing.Domain.Entities;
using DocProcessing.TestUtilities.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocProcessing.Application.Tests.Pipeline;

public sealed class OcrStageActivityTests
{
    private readonly Mock<IOcrService> _mockOcrService;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IDocumentService> _mockDocumentService;
    private readonly FakeLogger<OcrStageActivity> _logger;
    private readonly Mock<IOptions<OcrOptions>> _mockOptions;
    private readonly OcrStageActivity _sut;

    public OcrStageActivityTests()
    {
        _mockOcrService = new Mock<IOcrService>();
        _mockStorageService = new Mock<IStorageService>();
        _mockDocumentService = new Mock<IDocumentService>();
        _logger = new FakeLogger<OcrStageActivity>();
        _mockOptions = new Mock<IOptions<OcrOptions>>();

        _mockOptions.Setup(x => x.Value).Returns(new OcrOptions
        {
            Provider = "Mock",
            OutputBlobContainer = "ocr-results",
            TimeoutSeconds = 120,
            ModelId = "prebuilt-layout"
        });

        _sut = new OcrStageActivity(
            _mockOcrService.Object,
            _mockStorageService.Object,
            _mockDocumentService.Object,
            _logger,
            _mockOptions.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPdf_ReturnsSuccessWithOcrResults()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var blobPath = "documents/test.pdf";

        var stageContext = new StageContext(
            new ProcessJobModel(
                jobId,
                documentId,
                $"idempotency-{jobId}",
                ProcessJobStatus.Processing,
                ProcessJobStage.OCR),
            new Dictionary<string, object>
            {
                ["TenantId"] = tenantId,
                ["BlobPath"] = blobPath,
                ["BlobContainer"] = "documents"
            },
            $"correlation-{jobId}");

        var document = CreateTestDocument(documentId, tenantId, blobPath);
        var pdfStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]); // %PDF header
        var ocrResult = CreateTestOcrResult(documentId, jobId);

        _mockDocumentService
            .Setup(x => x.GetDocumentByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mockStorageService
            .Setup(x => x.DownloadBlobAsync("documents", blobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfStream);

        _mockOcrService
            .Setup(x => x.AnalyzeDocumentAsync(documentId, jobId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync("ocr-results", It.IsAny<string>(), ocrResult, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ocr-results/test-result.json");

        _mockDocumentService
            .Setup(x => x.UpdateDocumentMetadataAsync(documentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        var output = Assert.IsType<Dictionary<string, object>>(result.Output);
        Assert.Contains("pageCount", output);
        Assert.Contains("confidence", output);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPdf_StoresFullResultsInBlob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var blobPath = "documents/test.pdf";

        var stageContext = CreateStageContext(jobId, documentId, tenantId, blobPath);
        var document = CreateTestDocument(documentId, tenantId, blobPath);
        var pdfStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        var ocrResult = CreateTestOcrResult(documentId, jobId);

        SetupMocksForSuccessfulOcr(documentId, jobId, blobPath, document, pdfStream, ocrResult);

        // Act
        await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        _mockStorageService.Verify(
            x => x.UploadJsonAsync(
                "ocr-results",
                It.Is<string>(path => path.Contains(documentId.ToString())),
                It.Is<OcrResult>(r => r.DocumentId == documentId && r.JobId == jobId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPdf_StoresSummaryInDocumentMetadata()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var blobPath = "documents/test.pdf";

        var stageContext = CreateStageContext(jobId, documentId, tenantId, blobPath);
        var document = CreateTestDocument(documentId, tenantId, blobPath);
        var pdfStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        var ocrResult = CreateTestOcrResult(documentId, jobId);

        SetupMocksForSuccessfulOcr(documentId, jobId, blobPath, document, pdfStream, ocrResult);

        // Act
        await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        _mockDocumentService.Verify(
            x => x.UpdateDocumentMetadataAsync(
                documentId,
                It.Is<string>(json =>
                    json.Contains("ocrCompleted") &&
                    json.Contains("pageCount") &&
                    json.Contains("confidence")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidDocument_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";

        var stageContext = CreateStageContext(jobId, documentId, tenantId, "documents/test.pdf");

        _mockDocumentService
            .Setup(x => x.GetDocumentByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        // Act
        var result = await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBlobStorageFails_ReturnsFailureAndLogsError()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var blobPath = "documents/test.pdf";

        var stageContext = CreateStageContext(jobId, documentId, tenantId, blobPath);
        var document = CreateTestDocument(documentId, tenantId, blobPath);

        _mockDocumentService
            .Setup(x => x.GetDocumentByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mockStorageService
            .Setup(x => x.DownloadBlobAsync("documents", blobPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob not found"));

        // Act
        var result = await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorCode);
        _logger.VerifyWasCalled(LogLevel.Error, "OCR stage failed");
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsTextBlocksWithConfidence()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var blobPath = "documents/test.pdf";

        var stageContext = CreateStageContext(jobId, documentId, tenantId, blobPath);
        var document = CreateTestDocument(documentId, tenantId, blobPath);
        var pdfStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);

        // Create OCR result with text blocks
        var ocrResult = new OcrResult(
            documentId,
            jobId,
            new OcrMetadata("Mock", 1, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), 0.95),
            [
                new OcrPage(
                    1,
                    612,
                    792,
                    0.95,
                    [
                        new TextBlock("Sample text block", 0.98, 1, new BoundingBox(0.1, 0.1, 0.8, 0.2), "paragraph", "en"),
                        new TextBlock("Another text block", 0.92, 1, new BoundingBox(0.1, 0.3, 0.8, 0.2), "paragraph", "en")
                    ])
            ]);

        SetupMocksForSuccessfulOcr(documentId, jobId, blobPath, document, pdfStream, ocrResult);

        // Act
        var result = await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockStorageService.Verify(
            x => x.UploadJsonAsync(
                "ocr-results",
                It.IsAny<string>(),
                It.Is<OcrResult>(r =>
                    r.Pages.Count == 1 &&
                    r.Pages[0].TextBlocks.Count == 2 &&
                    r.Pages[0].TextBlocks[0].Confidence >= 0.9),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsTablesWithStructure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var blobPath = "documents/test.pdf";

        var stageContext = CreateStageContext(jobId, documentId, tenantId, blobPath);
        var document = CreateTestDocument(documentId, tenantId, blobPath);
        var pdfStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);

        // Create OCR result with tables
        var ocrResult = new OcrResult(
            documentId,
            jobId,
            new OcrMetadata("Mock", 1, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), 0.95, totalTables: 1),
            [
                new OcrPage(
                    1,
                    612,
                    792,
                    0.95,
                    tables: [
                        new TableData(
                            2,
                            3,
                            [
                                new TableCell(0, 0, "Header 1", 0.98, true),
                                new TableCell(0, 1, "Header 2", 0.97, true),
                                new TableCell(0, 2, "Header 3", 0.96, true),
                                new TableCell(1, 0, "Data 1", 0.95),
                                new TableCell(1, 1, "Data 2", 0.94),
                                new TableCell(1, 2, "Data 3", 0.93)
                            ],
                            1,
                            0.95)
                    ])
            ]);

        SetupMocksForSuccessfulOcr(documentId, jobId, blobPath, document, pdfStream, ocrResult);

        // Act
        var result = await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockStorageService.Verify(
            x => x.UploadJsonAsync(
                "ocr-results",
                It.IsAny<string>(),
                It.Is<OcrResult>(r =>
                    r.Pages[0].Tables.Count == 1 &&
                    r.Pages[0].Tables[0].RowCount == 2 &&
                    r.Pages[0].Tables[0].ColumnCount == 3 &&
                    r.Pages[0].Tables[0].Cells.Count == 6),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsKeyValuePairs()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var blobPath = "documents/test.pdf";

        var stageContext = CreateStageContext(jobId, documentId, tenantId, blobPath);
        var document = CreateTestDocument(documentId, tenantId, blobPath);
        var pdfStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);

        // Create OCR result with form fields
        var ocrResult = new OcrResult(
            documentId,
            jobId,
            new OcrMetadata("Mock", 1, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), 0.95, totalFormFields: 2),
            [
                new OcrPage(
                    1,
                    612,
                    792,
                    0.95,
                    formFields: [
                        new FormField("Invoice Number", "INV-12345", 0.98, 0.97, 1, fieldType: "text"),
                        new FormField("Total Amount", "$1,234.56", 0.96, 0.95, 1, fieldType: "currency")
                    ])
            ]);

        SetupMocksForSuccessfulOcr(documentId, jobId, blobPath, document, pdfStream, ocrResult);

        // Act
        var result = await _sut.ExecuteAsync(stageContext, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockStorageService.Verify(
            x => x.UploadJsonAsync(
                "ocr-results",
                It.IsAny<string>(),
                It.Is<OcrResult>(r =>
                    r.Pages[0].FormFields.Count == 2 &&
                    r.Pages[0].FormFields[0].Key == "Invoice Number" &&
                    r.Pages[0].FormFields[1].FieldType == "currency"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Helper methods

    private static StageContext CreateStageContext(Guid jobId, Guid documentId, string tenantId, string blobPath)
    {
        return new StageContext(
            new ProcessJobModel(
                jobId,
                documentId,
                $"idempotency-{jobId}",
                ProcessJobStatus.Processing,
                ProcessJobStage.OCR),
            new Dictionary<string, object>
            {
                ["TenantId"] = tenantId,
                ["BlobPath"] = blobPath,
                ["BlobContainer"] = "documents"
            },
            $"correlation-{jobId}");
    }

    private static Document CreateTestDocument(Guid documentId, string tenantId, string blobPath)
    {
        return new Document
        {
            DocumentId = documentId,
            TenantId = Guid.TryParse(tenantId, out var tid) ? tid : Guid.NewGuid(),
            FileName = "test.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            BlobContainer = "documents",
            BlobPath = blobPath,
            BlobETag = "etag-123",
            UploadedBy = "system",
            UploadedAtUtc = DateTime.UtcNow,
            Status = DocumentStatus.Uploaded
        };
    }

    private static OcrResult CreateTestOcrResult(Guid documentId, Guid jobId)
    {
        return new OcrResult(
            documentId,
            jobId,
            new OcrMetadata(
                "Mock",
                2,
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(5),
                0.95,
                totalTextBlocks: 10,
                totalTables: 1,
                totalFormFields: 3,
                primaryLanguage: "en",
                modelVersion: "1.0"),
            [
                new OcrPage(1, 612, 792, 0.96, [new TextBlock("Page 1 content", 0.96, 1)]),
                new OcrPage(2, 612, 792, 0.94, [new TextBlock("Page 2 content", 0.94, 2)])
            ]);
    }

    private void SetupMocksForSuccessfulOcr(
        Guid documentId,
        Guid jobId,
        string blobPath,
        Document document,
        Stream pdfStream,
        OcrResult ocrResult)
    {
        _mockDocumentService
            .Setup(x => x.GetDocumentByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mockStorageService
            .Setup(x => x.DownloadBlobAsync("documents", blobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfStream);

        _mockOcrService
            .Setup(x => x.AnalyzeDocumentAsync(documentId, jobId, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync("ocr-results", It.IsAny<string>(), It.IsAny<OcrResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ocr-results/test-result.json");

        _mockDocumentService
            .Setup(x => x.UpdateDocumentMetadataAsync(documentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}
