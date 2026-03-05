using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Models.Preprocessing;
using DocProcessing.Application.Pipeline;
using DocProcessing.Application.Services.Preprocessing;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DocProcessing.Application.Tests.Pipeline;

public sealed class PreprocessStageActivityTests
{
    private readonly FakeLogger<PreprocessStageActivity> _logger;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ITextNormalizer> _mockTextNormalizer;
    private readonly Mock<ITableConverter> _mockTableConverter;
    private readonly Mock<IFieldParser> _mockFieldParser;
    private readonly FakeTimeProvider _timeProvider;
    private readonly PreprocessOptions _options;
    private readonly PreprocessStageActivity _activity;

    public PreprocessStageActivityTests()
    {
        _logger = new FakeLogger<PreprocessStageActivity>();
        _mockStorageService = new Mock<IStorageService>();
        _mockTextNormalizer = new Mock<ITextNormalizer>();
        _mockTableConverter = new Mock<ITableConverter>();
        _mockFieldParser = new Mock<IFieldParser>();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));

        _options = new PreprocessOptions
        {
            OutputBlobContainer = "preprocess-results",
            EnableUnicodeNormalization = true,
            EnableWhitespaceCleanup = true,
            ConvertTablesToStructured = true
        };

        _activity = new PreprocessStageActivity(
            _logger,
            _mockStorageService.Object,
            Options.Create(_options),
            _timeProvider,
            _mockTextNormalizer.Object,
            _mockTableConverter.Object,
            _mockFieldParser.Object);
    }

    #region Happy Path Tests

    [Test]
    public async Task ExecuteAsync_WithValidOcrResults_ReturnsSuccessWithPreprocessedData()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var correlationId = "test-correlation-123";
        var ocrBlobPath = "ocr-results/tenant1/doc1/ocr-result.json";

        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 1);
        var context = CreateStageContext(jobId, documentId, correlationId, ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>("ocr-results", ocrBlobPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text.Trim());

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "preprocess-results",
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("preprocess-results/tenant1/doc1/preprocess-result.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Output).IsNull();
        await Assert.That(result.Metadata).IsNotNull();
        await Assert.That(result.Metadata.Keys).Contains(StageMetadataKeys.PreprocessBlobPath);

        _mockStorageService.Verify(
            x => x.DownloadJsonAsync<OcrResult>("ocr-results", ocrBlobPath, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockStorageService.Verify(
            x => x.UploadJsonAsync(
                "preprocess-results",
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithMultiplePages_NormalizesAllPages()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 3);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text.Trim());

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        // Verify text normalizer was called for each page's text blocks
        _mockTextNormalizer.Verify(
            x => x.NormalizeText(It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task ExecuteAsync_WithTablesAndFormFields_ConvertsToStructuredFormat()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var ocrResult = CreateOcrResultWithTablesAndFormFields(jobId, documentId);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockTableConverter
            .Setup(x => x.ConvertToStructured(It.IsAny<TableData>()))
            .Returns(new StructuredTable
            {
                TableNumber = 1,
                PageNumber = 1,
                Headers = new[] { "Header1", "Header2" },
                Rows = new[] { new Dictionary<string, string> { ["Header1"] = "Value1" } },
                JsonRepresentation = "{}",
                CsvRepresentation = "Header1,Header2",
                Confidence = 0.95,
                BoundingBox = null
            });

        _mockFieldParser
            .Setup(x => x.ParseField(It.IsAny<FormField>()))
            .Returns(new NormalizedFormField
            {
                Key = "TestKey",
                OriginalValue = "TestValue",
                NormalizedValue = "TestValue",
                FieldType = "text",
                ParsedValue = "TestValue",
                KeyConfidence = 0.9,
                ValueConfidence = 0.9,
                PageNumber = 1
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockTableConverter.Verify(
            x => x.ConvertToStructured(It.IsAny<TableData>()),
            Times.AtLeastOnce);

        _mockFieldParser.Verify(
            x => x.ParseField(It.IsAny<FormField>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task ExecuteAsync_StoresResultInBlobStorage_AndReturnsCorrectMetadata()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var tenantId = "tenant-123";
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var expectedBlobPath = $"preprocess-results/{tenantId}/{documentId}/preprocess-result.json";

        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 1);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath, tenantId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "preprocess-results",
                It.Is<string>(path => path.Contains(tenantId) && path.Contains(documentId.ToString())),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBlobPath);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata.Keys).Contains(StageMetadataKeys.PreprocessBlobPath);
        await Assert.That(result.Metadata[StageMetadataKeys.PreprocessBlobPath]).IsEqualTo(expectedBlobPath);
        await Assert.That(result.Metadata.Keys).Contains("pageCount");
        await Assert.That(result.Metadata.Keys).Contains("totalWordCount");
    }

    #endregion

    #region Text Normalization Tests

    [Test]
    public async Task ExecuteAsync_WithExtraWhitespace_NormalizesWhitespace()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var textWithWhitespace = "This  is   some    text";
        var normalizedText = "This is some text";

        var ocrResult = CreateOcrResultWithText(jobId, documentId, textWithWhitespace);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(textWithWhitespace))
            .Returns(normalizedText);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTextNormalizer.Verify(x => x.NormalizeText(textWithWhitespace), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithUnicodeCharacters_NormalizesToNFC()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        // NFD decomposed form: e + combining acute accent
        var unnormalizedText = "caf\u0065\u0301";
        // NFC composed form
        var normalizedText = "café";

        var ocrResult = CreateOcrResultWithText(jobId, documentId, unnormalizedText);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(unnormalizedText))
            .Returns(normalizedText);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTextNormalizer.Verify(x => x.NormalizeText(unnormalizedText), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleLineBreaks_CollapsesToSingleBreaks()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var textWithBreaks = "Line1\n\n\nLine2\n\n\n\nLine3";
        var normalizedText = "Line1\nLine2\nLine3";

        var ocrResult = CreateOcrResultWithText(jobId, documentId, textWithBreaks);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(textWithBreaks))
            .Returns(normalizedText);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTextNormalizer.Verify(x => x.NormalizeText(textWithBreaks), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithLigatures_ExpandsToRegularCharacters()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var textWithLigatures = "ﬁle";  // Contains fi ligature (U+FB01)
        var normalizedText = "file";

        var ocrResult = CreateOcrResultWithText(jobId, documentId, textWithLigatures);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(textWithLigatures))
            .Returns(normalizedText);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTextNormalizer.Verify(x => x.NormalizeText(textWithLigatures), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithMixedNewlines_NormalizesToConsistentFormat()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var textWithMixedNewlines = "Line1\r\nLine2\rLine3\nLine4";
        var normalizedText = "Line1\nLine2\nLine3\nLine4";

        var ocrResult = CreateOcrResultWithText(jobId, documentId, textWithMixedNewlines);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(textWithMixedNewlines))
            .Returns(normalizedText);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTextNormalizer.Verify(x => x.NormalizeText(textWithMixedNewlines), Times.Once);
    }

    #endregion

    #region Table Conversion Tests

    [Test]
    public async Task ExecuteAsync_WithSimpleTable_GeneratesJsonAndCsv()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var table = CreateSimpleTable();
        var ocrResult = CreateOcrResultWithTable(jobId, documentId, table);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        var structuredTable = new StructuredTable
        {
            TableNumber = 1,
            PageNumber = 1,
            Headers = new[] { "Name", "Age" },
            Rows = new[]
            {
                new Dictionary<string, string> { ["Name"] = "John", ["Age"] = "30" }
            },
            JsonRepresentation = "[{\"Name\":\"John\",\"Age\":\"30\"}]",
            CsvRepresentation = "Name,Age\nJohn,30",
            Confidence = 0.95,
            BoundingBox = null
        };

        _mockTableConverter
            .Setup(x => x.ConvertToStructured(table))
            .Returns(structuredTable);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTableConverter.Verify(x => x.ConvertToStructured(table), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithTableHeaders_ExtractsHeadersCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var table = CreateTableWithHeaders();
        var ocrResult = CreateOcrResultWithTable(jobId, documentId, table);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        var expectedHeaders = new[] { "Product", "Price", "Quantity" };
        var structuredTable = new StructuredTable
        {
            TableNumber = 1,
            PageNumber = 1,
            Headers = expectedHeaders,
            Rows = new[]
            {
                new Dictionary<string, string>
                {
                    ["Product"] = "Widget",
                    ["Price"] = "$10.00",
                    ["Quantity"] = "5"
                }
            },
            JsonRepresentation = "[]",
            CsvRepresentation = "",
            Confidence = 0.9,
            BoundingBox = null
        };

        _mockTableConverter
            .Setup(x => x.ConvertToStructured(table))
            .Returns(structuredTable);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTableConverter.Verify(x => x.ConvertToStructured(table), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithSpannedCells_HandlesSpansCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var table = CreateTableWithSpannedCells();
        var ocrResult = CreateOcrResultWithTable(jobId, documentId, table);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockTableConverter
            .Setup(x => x.ConvertToStructured(table))
            .Returns(new StructuredTable
            {
                TableNumber = 1,
                PageNumber = 1,
                Headers = new[] { "Col1", "Col2" },
                Rows = new[]
                {
                    new Dictionary<string, string> { ["Col1"] = "Spanned", ["Col2"] = "Value" }
                },
                JsonRepresentation = "[]",
                CsvRepresentation = "",
                Confidence = 0.85,
                BoundingBox = null
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTableConverter.Verify(x => x.ConvertToStructured(It.IsAny<TableData>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithNumericTable_PreservesNumericFormats()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var table = CreateNumericTable();
        var ocrResult = CreateOcrResultWithTable(jobId, documentId, table);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockTableConverter
            .Setup(x => x.ConvertToStructured(table))
            .Returns(new StructuredTable
            {
                TableNumber = 1,
                PageNumber = 1,
                Headers = new[] { "Amount", "Percentage" },
                Rows = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["Amount"] = "1,234.56",
                        ["Percentage"] = "45.67%"
                    }
                },
                JsonRepresentation = "[]",
                CsvRepresentation = "Amount,Percentage\n\"1,234.56\",\"45.67%\"",
                Confidence = 0.92,
                BoundingBox = null
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockTableConverter.Verify(x => x.ConvertToStructured(table), Times.Once);
    }

    #endregion

    #region Form Field Parsing Tests

    [Test]
    public async Task ExecuteAsync_WithDateFields_ParsesAndNormalizesDates()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var formField = new FormField(
            key: "InvoiceDate",
            value: "01/15/2024",
            keyConfidence: 0.95,
            valueConfidence: 0.90,
            pageNumber: 1,
            keyBoundingBox: new BoundingBox(0, 0, 0.1, 0.05),
            valueBoundingBox: new BoundingBox(0.2, 0, 0.15, 0.05),
            fieldType: "date");

        var ocrResult = CreateOcrResultWithFormField(jobId, documentId, formField);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        var parsedDate = new DateTime(2024, 1, 15);
        _mockFieldParser
            .Setup(x => x.ParseField(formField))
            .Returns(new NormalizedFormField
            {
                Key = "InvoiceDate",
                OriginalValue = "01/15/2024",
                NormalizedValue = "2024-01-15",
                FieldType = "date",
                ParsedValue = parsedDate,
                KeyConfidence = 0.95,
                ValueConfidence = 0.90,
                PageNumber = 1
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockFieldParser.Verify(x => x.ParseField(formField), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithCurrencyFields_ParsesAndNormalizesCurrency()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var formField = new FormField(
            key: "TotalAmount",
            value: "$1,234.56",
            keyConfidence: 0.98,
            valueConfidence: 0.95,
            pageNumber: 1,
            keyBoundingBox: new BoundingBox(0, 0, 0.1, 0.05),
            valueBoundingBox: new BoundingBox(0.2, 0, 0.15, 0.05),
            fieldType: "currency");

        var ocrResult = CreateOcrResultWithFormField(jobId, documentId, formField);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockFieldParser
            .Setup(x => x.ParseField(formField))
            .Returns(new NormalizedFormField
            {
                Key = "TotalAmount",
                OriginalValue = "$1,234.56",
                NormalizedValue = "1234.56",
                FieldType = "currency",
                ParsedValue = 1234.56m,
                KeyConfidence = 0.98,
                ValueConfidence = 0.95,
                PageNumber = 1
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockFieldParser.Verify(x => x.ParseField(formField), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithNumericFields_ParsesNumbers()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var formField = new FormField(
            key: "Quantity",
            value: "1,000",
            keyConfidence: 0.99,
            valueConfidence: 0.97,
            pageNumber: 1,
            keyBoundingBox: new BoundingBox(0, 0, 0.1, 0.05),
            valueBoundingBox: new BoundingBox(0.2, 0, 0.15, 0.05),
            fieldType: "number");

        var ocrResult = CreateOcrResultWithFormField(jobId, documentId, formField);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockFieldParser
            .Setup(x => x.ParseField(formField))
            .Returns(new NormalizedFormField
            {
                Key = "Quantity",
                OriginalValue = "1,000",
                NormalizedValue = "1000",
                FieldType = "number",
                ParsedValue = 1000,
                KeyConfidence = 0.99,
                ValueConfidence = 0.97,
                PageNumber = 1
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockFieldParser.Verify(x => x.ParseField(formField), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidDateFormat_StoresOriginalValue()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var formField = new FormField(
            key: "InvalidDate",
            value: "not-a-date",
            keyConfidence: 0.85,
            valueConfidence: 0.70,
            pageNumber: 1,
            keyBoundingBox: new BoundingBox(0, 0, 0.1, 0.05),
            valueBoundingBox: new BoundingBox(0.2, 0, 0.15, 0.05),
            fieldType: "date");

        var ocrResult = CreateOcrResultWithFormField(jobId, documentId, formField);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockFieldParser
            .Setup(x => x.ParseField(formField))
            .Returns(new NormalizedFormField
            {
                Key = "InvalidDate",
                OriginalValue = "not-a-date",
                NormalizedValue = "not-a-date",  // Keep original when parse fails
                FieldType = "text",  // Fallback to text type
                ParsedValue = null,
                KeyConfidence = 0.85,
                ValueConfidence = 0.70,
                PageNumber = 1
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockFieldParser.Verify(x => x.ParseField(formField), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithMixedFieldTypes_ParsesAllCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var formFields = new List<FormField>
        {
            new FormField(
                key: "Date",
                value: "2024-01-15",
                keyConfidence: 0.9,
                valueConfidence: 0.9,
                pageNumber: 1,
                keyBoundingBox: new BoundingBox(0, 0, 0.1, 0.05),
                valueBoundingBox: new BoundingBox(0.2, 0, 0.15, 0.05),
                fieldType: "date"),
            new FormField(
                key: "Amount",
                value: "$100.00",
                keyConfidence: 0.95,
                valueConfidence: 0.92,
                pageNumber: 1,
                keyBoundingBox: new BoundingBox(0, 0.1, 0.1, 0.05),
                valueBoundingBox: new BoundingBox(0.2, 0.1, 0.15, 0.05),
                fieldType: "currency"),
            new FormField(
                key: "Name",
                value: "John Doe",
                keyConfidence: 0.98,
                valueConfidence: 0.96,
                pageNumber: 1,
                keyBoundingBox: new BoundingBox(0, 0.2, 0.1, 0.05),
                valueBoundingBox: new BoundingBox(0.2, 0.2, 0.15, 0.05),
                fieldType: "text")
        };

        var ocrResult = CreateOcrResultWithFormFields(jobId, documentId, formFields);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockFieldParser
            .Setup(x => x.ParseField(It.IsAny<FormField>()))
            .Returns((FormField field) => new NormalizedFormField
            {
                Key = field.Key,
                OriginalValue = field.Value,
                NormalizedValue = field.Value,
                FieldType = field.FieldType ?? "text",
                ParsedValue = field.Value,
                KeyConfidence = field.KeyConfidence,
                ValueConfidence = field.ValueConfidence,
                PageNumber = field.PageNumber
            });

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockFieldParser.Verify(x => x.ParseField(It.IsAny<FormField>()), Times.Exactly(3));
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ExecuteAsync_WhenOcrBlobPathMissing_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath: null);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("PREPROCESS_MISSING_OCR_PATH");
        await Assert.That(result.ErrorMessage).Contains("OCR blob path not found");
    }

    [Test]
    public async Task ExecuteAsync_WhenOcrResultNotFound_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OcrResult?)null);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("PREPROCESS_OCR_NOT_FOUND");
        await Assert.That(result.ErrorMessage).Contains("OCR result not found");
    }

    [Test]
    public async Task ExecuteAsync_WhenBlobStorageFails_ReturnsFailureWithErrorCode()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob storage connection failed"));

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("PREPROCESS_ERROR");
        await Assert.That(result.ErrorMessage).Contains("Blob storage connection failed");
    }

    [Test]
    public async Task ExecuteAsync_WhenProcessingThrows_ReturnsFailureWithException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 1);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Throws(new ArgumentException("Invalid text format"));

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("PREPROCESS_ERROR");
        await Assert.That(result.ErrorMessage).Contains("Invalid text format");
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public async Task ExecuteAsync_WithEmptyOcrResult_ReturnsSuccessWithEmptyData()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var emptyOcrResult = new OcrResult(
            documentId: documentId,
            jobId: jobId,
            metadata: new OcrMetadata(
                provider: "Mock",
                pageCount: 0,
                processedAt: DateTimeOffset.UtcNow,
                processingDuration: TimeSpan.FromSeconds(1),
                overallConfidence: 0,
                totalTextBlocks: 0,
                totalTables: 0,
                totalFormFields: 0,
                primaryLanguage: "en",
                modelVersion: "1.0",
                status: "Success",
                warnings: []),
            pages: [],
            blobPath: ocrBlobPath);

        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyOcrResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata).IsNotNull();
        await Assert.That(result.Metadata["pageCount"]).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteAsync_WithZeroConfidence_StillProcesses()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 1, confidence: 0.0);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WithNullMetadata_InitializesMetadata()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var metadata = new Dictionary<string, object>
        {
            [StageMetadataKeys.OcrBlobPath] = ocrBlobPath
        };

        var context = new StageContext(
            new ProcessJobModel(jobId, documentId, "idem-key", ProcessJobStatus.Processing, ProcessJobStage.Preprocess),
            metadata,
            "corr-123");

        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 1);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _activity.ExecuteAsync(context, cts.Token);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("PREPROCESS_ERROR");
    }

    #endregion

    #region Logging Verification Tests

    [Test]
    public async Task ExecuteAsync_LogsStartAndCompletion_WithCorrectCorrelationId()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var correlationId = "test-correlation-456";
        var ocrBlobPath = "ocr-results/test/ocr.json";

        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 1);
        var context = CreateStageContext(jobId, documentId, correlationId, ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs).Contains(log =>
            log.Level == LogLevel.Information &&
            log.Message.Contains("Starting Preprocess stage") &&
            log.Message.Contains(correlationId));
        await Assert.That(logs).Contains(log =>
            log.Level == LogLevel.Information &&
            log.Message.Contains("Preprocess stage completed"));
    }

    [Test]
    public async Task ExecuteAsync_OnFailure_LogsErrorWithJobId()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs).Contains(log =>
            log.Level == LogLevel.Error &&
            log.Message.Contains("Preprocess stage failed") &&
            log.Message.Contains(jobId.ToString()));
    }

    [Test]
    public async Task ExecuteAsync_LogsProcessingStatistics_InMetadata()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var ocrBlobPath = "ocr-results/test/ocr.json";
        var ocrResult = CreateSampleOcrResult(jobId, documentId, pageCount: 2);
        var context = CreateStageContext(jobId, documentId, "corr-123", ocrBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<OcrResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        _mockTextNormalizer
            .Setup(x => x.NormalizeText(It.IsAny<string>()))
            .Returns((string text) => text);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PreprocessResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob-path");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs).Contains(log =>
            log.Level == LogLevel.Information &&
            log.Message.Contains("Preprocess stage completed"));

        await Assert.That(result.Metadata).IsNotNull();
        await Assert.That(result.Metadata.Keys).Contains("pageCount");
        await Assert.That(result.Metadata.Keys).Contains("totalWordCount");
    }

    #endregion

    #region Helper Methods

    private static StageContext CreateStageContext(
        Guid jobId,
        Guid documentId,
        string correlationId,
        string? ocrBlobPath,
        string? tenantId = null)
    {
        var metadata = new Dictionary<string, object>();

        if (ocrBlobPath != null)
        {
            metadata[StageMetadataKeys.OcrBlobPath] = ocrBlobPath;
        }

        if (tenantId != null)
        {
            metadata[StageMetadataKeys.TenantId] = tenantId;
        }

        return new StageContext(
            new ProcessJobModel(jobId, documentId, "idem-key", ProcessJobStatus.Processing, ProcessJobStage.Preprocess),
            metadata,
            correlationId);
    }

    private static OcrResult CreateSampleOcrResult(Guid jobId, Guid documentId, int pageCount, double confidence = 0.95)
    {
        var pages = new List<OcrPage>();

        for (int i = 1; i <= pageCount; i++)
        {
            pages.Add(new OcrPage(
                pageNumber: i,
                width: 612,
                height: 792,
                confidence: confidence,
                textBlocks: [
                    new TextBlock(
                        text: $"Sample text on page {i}",
                        confidence: confidence,
                        pageNumber: i,
                        boundingBox: new BoundingBox(0.1, 0.1, 0.8, 0.1),
                        blockType: "paragraph",
                        languageCode: "en")
                ],
                tables: [],
                formFields: [],
                language: "en",
                angle: 0));
        }

        return new OcrResult(
            documentId: documentId,
            jobId: jobId,
            metadata: new OcrMetadata(
                provider: "Mock",
                pageCount: pageCount,
                processedAt: DateTimeOffset.UtcNow,
                processingDuration: TimeSpan.FromSeconds(5),
                overallConfidence: confidence,
                totalTextBlocks: pageCount,
                totalTables: 0,
                totalFormFields: 0,
                primaryLanguage: "en",
                modelVersion: "1.0",
                status: "Success",
                warnings: []),
            pages: pages,
            blobPath: "ocr-results/test/ocr.json");
    }

    private static OcrResult CreateOcrResultWithText(Guid jobId, Guid documentId, string text)
    {
        var result = CreateSampleOcrResult(jobId, documentId, pageCount: 1);

        return new OcrResult(
            documentId: documentId,
            jobId: jobId,
            metadata: result.Metadata,
            pages: [
                new OcrPage(
                    pageNumber: 1,
                    width: 612,
                    height: 792,
                    confidence: 0.9,
                    textBlocks: [
                        new TextBlock(
                            text: text,
                            confidence: 0.9,
                            pageNumber: 1,
                            boundingBox: new BoundingBox(0.1, 0.1, 0.8, 0.1),
                            blockType: "paragraph",
                            languageCode: "en")
                    ],
                    tables: [],
                    formFields: [],
                    language: "en",
                    angle: 0)
            ],
            blobPath: "ocr-results/test/ocr.json");
    }

    private static OcrResult CreateOcrResultWithTablesAndFormFields(Guid jobId, Guid documentId)
    {
        var table = CreateSimpleTable();
        var formField = new FormField(
            key: "TestKey",
            value: "TestValue",
            keyConfidence: 0.9,
            valueConfidence: 0.9,
            pageNumber: 1,
            keyBoundingBox: new BoundingBox(0, 0, 0.1, 0.05),
            valueBoundingBox: new BoundingBox(0.2, 0, 0.15, 0.05),
            fieldType: "text");

        return new OcrResult(
            documentId: documentId,
            jobId: jobId,
            metadata: new OcrMetadata(
                provider: "Mock",
                pageCount: 1,
                processedAt: DateTimeOffset.UtcNow,
                processingDuration: TimeSpan.FromSeconds(5),
                overallConfidence: 0.9,
                totalTextBlocks: 1,
                totalTables: 1,
                totalFormFields: 1,
                primaryLanguage: "en",
                modelVersion: "1.0",
                status: "Success",
                warnings: []),
            pages: [
                new OcrPage(
                    pageNumber: 1,
                    width: 612,
                    height: 792,
                    confidence: 0.9,
                    textBlocks: [
                        new TextBlock(
                            text: "Sample text",
                            confidence: 0.9,
                            pageNumber: 1,
                            boundingBox: new BoundingBox(0.1, 0.1, 0.8, 0.1),
                            blockType: "paragraph",
                            languageCode: "en")
                    ],
                    tables: [table],
                    formFields: [formField],
                    language: "en",
                    angle: 0)
            ],
            blobPath: "ocr-results/test/ocr.json");
    }

    private static OcrResult CreateOcrResultWithTable(Guid jobId, Guid documentId, TableData table)
    {
        return new OcrResult(
            documentId: documentId,
            jobId: jobId,
            metadata: new OcrMetadata(
                provider: "Mock",
                pageCount: 1,
                processedAt: DateTimeOffset.UtcNow,
                processingDuration: TimeSpan.FromSeconds(5),
                overallConfidence: 0.9,
                totalTextBlocks: 0,
                totalTables: 1,
                totalFormFields: 0,
                primaryLanguage: "en",
                modelVersion: "1.0",
                status: "Success",
                warnings: []),
            pages: [
                new OcrPage(
                    pageNumber: 1,
                    width: 612,
                    height: 792,
                    confidence: 0.9,
                    textBlocks: [],
                    tables: [table],
                    formFields: [],
                    language: "en",
                    angle: 0)
            ],
            blobPath: "ocr-results/test/ocr.json");
    }

    private static OcrResult CreateOcrResultWithFormField(Guid jobId, Guid documentId, FormField formField)
    {
        return new OcrResult(
            documentId: documentId,
            jobId: jobId,
            metadata: new OcrMetadata(
                provider: "Mock",
                pageCount: 1,
                processedAt: DateTimeOffset.UtcNow,
                processingDuration: TimeSpan.FromSeconds(5),
                overallConfidence: 0.9,
                totalTextBlocks: 0,
                totalTables: 0,
                totalFormFields: 1,
                primaryLanguage: "en",
                modelVersion: "1.0",
                status: "Success",
                warnings: []),
            pages: [
                new OcrPage(
                    pageNumber: 1,
                    width: 612,
                    height: 792,
                    confidence: 0.9,
                    textBlocks: [],
                    tables: [],
                    formFields: [formField],
                    language: "en",
                    angle: 0)
            ],
            blobPath: "ocr-results/test/ocr.json");
    }

    private static OcrResult CreateOcrResultWithFormFields(Guid jobId, Guid documentId, List<FormField> formFields)
    {
        return new OcrResult(
            documentId: documentId,
            jobId: jobId,
            metadata: new OcrMetadata(
                provider: "Mock",
                pageCount: 1,
                processedAt: DateTimeOffset.UtcNow,
                processingDuration: TimeSpan.FromSeconds(5),
                overallConfidence: 0.9,
                totalTextBlocks: 0,
                totalTables: 0,
                totalFormFields: formFields.Count,
                primaryLanguage: "en",
                modelVersion: "1.0",
                status: "Success",
                warnings: []),
            pages: [
                new OcrPage(
                    pageNumber: 1,
                    width: 612,
                    height: 792,
                    confidence: 0.9,
                    textBlocks: [],
                    tables: [],
                    formFields: formFields,
                    language: "en",
                    angle: 0)
            ],
            blobPath: "ocr-results/test/ocr.json");
    }

    private static TableData CreateSimpleTable()
    {
        var cells = new List<TableCell>
        {
            new TableCell(0, 0, "Name", 0.95, isHeader: true, boundingBox: new BoundingBox(0, 0, 0.5, 0.1)),
            new TableCell(0, 1, "Age", 0.95, isHeader: true, boundingBox: new BoundingBox(0.5, 0, 0.5, 0.1)),
            new TableCell(1, 0, "John", 0.90, isHeader: false, boundingBox: new BoundingBox(0, 0.1, 0.5, 0.1)),
            new TableCell(1, 1, "30", 0.90, isHeader: false, boundingBox: new BoundingBox(0.5, 0.1, 0.5, 0.1))
        };

        return new TableData(
            rowCount: 2,
            columnCount: 2,
            cells: cells,
            pageNumber: 1,
            confidence: 0.95,
            boundingBox: new BoundingBox(0, 0, 1, 0.2));
    }

    private static TableData CreateTableWithHeaders()
    {
        var cells = new List<TableCell>
        {
            new TableCell(0, 0, "Product", 0.95, isHeader: true, boundingBox: new BoundingBox(0, 0, 0.33, 0.1)),
            new TableCell(0, 1, "Price", 0.95, isHeader: true, boundingBox: new BoundingBox(0.33, 0, 0.33, 0.1)),
            new TableCell(0, 2, "Quantity", 0.95, isHeader: true, boundingBox: new BoundingBox(0.66, 0, 0.34, 0.1)),
            new TableCell(1, 0, "Widget", 0.90, isHeader: false, boundingBox: new BoundingBox(0, 0.1, 0.33, 0.1)),
            new TableCell(1, 1, "$10.00", 0.90, isHeader: false, boundingBox: new BoundingBox(0.33, 0.1, 0.33, 0.1)),
            new TableCell(1, 2, "5", 0.90, isHeader: false, boundingBox: new BoundingBox(0.66, 0.1, 0.34, 0.1))
        };

        return new TableData(
            rowCount: 2,
            columnCount: 3,
            cells: cells,
            pageNumber: 1,
            confidence: 0.9,
            boundingBox: new BoundingBox(0, 0, 1, 0.2));
    }

    private static TableData CreateTableWithSpannedCells()
    {
        var cells = new List<TableCell>
        {
            new TableCell(0, 0, "Spanned", 0.85, isHeader: false, rowSpan: 1, columnSpan: 2, boundingBox: new BoundingBox(0, 0, 1, 0.1)),
            new TableCell(1, 0, "Value1", 0.90, isHeader: false, boundingBox: new BoundingBox(0, 0.1, 0.5, 0.1)),
            new TableCell(1, 1, "Value2", 0.90, isHeader: false, boundingBox: new BoundingBox(0.5, 0.1, 0.5, 0.1))
        };

        return new TableData(
            rowCount: 2,
            columnCount: 2,
            cells: cells,
            pageNumber: 1,
            confidence: 0.85,
            boundingBox: new BoundingBox(0, 0, 1, 0.2));
    }

    private static TableData CreateNumericTable()
    {
        var cells = new List<TableCell>
        {
            new TableCell(0, 0, "Amount", 0.95, isHeader: true, boundingBox: new BoundingBox(0, 0, 0.5, 0.1)),
            new TableCell(0, 1, "Percentage", 0.95, isHeader: true, boundingBox: new BoundingBox(0.5, 0, 0.5, 0.1)),
            new TableCell(1, 0, "1,234.56", 0.92, isHeader: false, boundingBox: new BoundingBox(0, 0.1, 0.5, 0.1)),
            new TableCell(1, 1, "45.67%", 0.92, isHeader: false, boundingBox: new BoundingBox(0.5, 0.1, 0.5, 0.1))
        };

        return new TableData(
            rowCount: 2,
            columnCount: 2,
            cells: cells,
            pageNumber: 1,
            confidence: 0.92,
            boundingBox: new BoundingBox(0, 0, 1, 0.2));
    }

    #endregion
}
