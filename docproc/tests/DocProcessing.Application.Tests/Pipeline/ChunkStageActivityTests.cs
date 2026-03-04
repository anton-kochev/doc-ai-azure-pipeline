using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models;
using DocProcessing.Application.Models.Chunking;
using DocProcessing.Application.Models.Preprocessing;
using DocProcessing.Application.Pipeline;
using DocProcessing.Application.Services.Chunking;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DocProcessing.Application.Tests.Pipeline;

public sealed class ChunkStageActivityTests
{
    private readonly FakeLogger<ChunkStageActivity> _logger;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IDocumentChunker> _mockDocumentChunker;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ChunkingOptions _options;
    private readonly ChunkStageActivity _activity;

    public ChunkStageActivityTests()
    {
        _logger = new FakeLogger<ChunkStageActivity>();
        _mockStorageService = new Mock<IStorageService>();
        _mockDocumentChunker = new Mock<IDocumentChunker>();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));

        _options = new ChunkingOptions
        {
            OutputBlobContainer = "chunk-results",
            MaxChunkSize = 512,
            OverlapTokens = 50,
            TokenEstimationFactor = 1.3
        };

        _activity = new ChunkStageActivity(
            _logger,
            _mockStorageService.Object,
            Options.Create(_options),
            _timeProvider,
            _mockDocumentChunker.Object);
    }

    #region Happy Path Tests

    [Test]
    public async Task ExecuteAsync_WithValidPreprocessResult_ReturnsSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-001", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        SetupDefaultChunkerResult(documentId, preprocessResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-results/tenant1/doc1/chunk-result.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Output).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_Success_UploadsChunkResultToBlob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-002", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        SetupDefaultChunkerResult(documentId, preprocessResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-results/blob-path.json");

        // Act
        await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        _mockStorageService.Verify(
            x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_Success_MetadataContainsChunkBlobPath()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var expectedChunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-003", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        SetupDefaultChunkerResult(documentId, preprocessResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChunkBlobPath);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.ChunkBlobPath)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.ChunkBlobPath]).IsEqualTo(expectedChunkBlobPath);
    }

    [Test]
    public async Task ExecuteAsync_Success_MetadataContainsTotalChunks()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-004", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        var chunks = new List<DocumentChunk>
        {
            CreateDocumentChunk(documentId, 0, ChunkType.Text),
            CreateDocumentChunk(documentId, 1, ChunkType.Text),
            CreateDocumentChunk(documentId, 2, ChunkType.Table)
        };
        var chunkMetadata = new ChunkMetadata
        {
            TotalChunks = 3,
            TextChunks = 2,
            TableChunks = 1,
            FormFieldChunks = 0,
            TotalTokens = 300,
            MaxChunkSize = _options.MaxChunkSize,
            OverlapTokens = _options.OverlapTokens
        };

        _mockDocumentChunker
            .Setup(x => x.ChunkDocument(preprocessResult, It.IsAny<ChunkingOptions>()))
            .Returns((chunks, chunkMetadata));

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-results/blob.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.TotalChunks)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.TotalChunks]).IsEqualTo(3);
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.TextChunks)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.TextChunks]).IsEqualTo(2);
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.TableChunks)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.TableChunks]).IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_Success_MetadataContainsTotalTokens()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-005", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        var chunkMetadata = new ChunkMetadata
        {
            TotalChunks = 2,
            TextChunks = 2,
            TableChunks = 0,
            FormFieldChunks = 0,
            TotalTokens = 750,
            MaxChunkSize = _options.MaxChunkSize,
            OverlapTokens = _options.OverlapTokens
        };

        _mockDocumentChunker
            .Setup(x => x.ChunkDocument(preprocessResult, It.IsAny<ChunkingOptions>()))
            .Returns((new List<DocumentChunk>
            {
                CreateDocumentChunk(documentId, 0, ChunkType.Text),
                CreateDocumentChunk(documentId, 1, ChunkType.Text)
            }, chunkMetadata));

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-results/blob.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.TotalTokens)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.TotalTokens]).IsEqualTo(750);
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.ProcessingDurationMs)).IsTrue();
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ExecuteAsync_MissingPreprocessBlobPath_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var context = CreateStageContext(jobId, documentId, "corr-006", preprocessBlobPath: null);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("CHUNK_MISSING_PREPROCESS_PATH");
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_PreprocessBlobPathNotString_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            [StageMetadataKeys.PreprocessBlobPath] = 42  // non-string value
        };
        var context = new StageContext(
            new ProcessJobModel(jobId, documentId, "idem-key", ProcessJobStatus.Processing, ProcessJobStage.Chunk),
            metadata,
            "corr-007");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("CHUNK_MISSING_PREPROCESS_PATH");
    }

    [Test]
    public async Task ExecuteAsync_PreprocessResultNotFound_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-008", preprocessBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PreprocessResult?)null);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("CHUNK_PREPROCESS_NOT_FOUND");
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_ChunkerThrowsException_ReturnsChunkError()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-009", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        _mockDocumentChunker
            .Setup(x => x.ChunkDocument(It.IsAny<PreprocessResult>(), It.IsAny<ChunkingOptions>()))
            .Throws(new InvalidOperationException("Chunker internal failure"));

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("CHUNK_ERROR");
        await Assert.That(result.ErrorMessage).Contains("unexpected error");
    }

    [Test]
    public async Task ExecuteAsync_UploadFailure_ReturnsChunkError()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-010", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        SetupDefaultChunkerResult(documentId, preprocessResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob storage upload failed"));

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("CHUNK_ERROR");
        await Assert.That(result.ErrorMessage).Contains("unexpected error");
    }

    #endregion

    #region Guards and Properties Tests

    [Test]
    public async Task ExecuteAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(async () => { await _activity.ExecuteAsync(null!); }).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task StageName_ReturnsChunk()
    {
        // Act & Assert
        await Assert.That(_activity.StageName).IsEqualTo("Chunk");
    }

    [Test]
    public async Task Stage_ReturnsProcessJobStageChunk()
    {
        // Act & Assert
        await Assert.That(_activity.Stage).IsEqualTo(ProcessJobStage.Chunk);
    }

    #endregion

    #region Logging Tests

    [Test]
    public async Task ExecuteAsync_LogsStageStartWithJobIdAndCorrelationId()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var correlationId = "corr-log-start-001";
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, correlationId, preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        SetupDefaultChunkerResult(documentId, preprocessResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-results/blob.json");

        // Act
        await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs.Any(l =>
            l.Level == LogLevel.Information &&
            l.Message.Contains(jobId.ToString()) &&
            l.Message.Contains(correlationId))).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_Success_LogsCompletionWithStats()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-log-complete-001", preprocessBlobPath);
        var preprocessResult = CreateSamplePreprocessResult(jobId, documentId);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preprocessResult);

        SetupDefaultChunkerResult(documentId, preprocessResult);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-results/blob.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs.Any(l =>
            l.Level == LogLevel.Information &&
            l.Message.Contains(jobId.ToString()))).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_Failure_LogsErrorWithJobIdAndCorrelationId()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var correlationId = "corr-log-error-001";
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, correlationId, preprocessBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage failure"));

        // Act
        await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs.Any(l =>
            l.Level == LogLevel.Error &&
            l.Message.Contains(jobId.ToString()))).IsTrue();
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_PassesToStorageService()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var preprocessBlobPath = "preprocess-results/tenant1/doc1/preprocess-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-cancel-001", preprocessBlobPath);
        var cts = new CancellationTokenSource();

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                cts.Token))
            .ReturnsAsync(CreateSamplePreprocessResult(jobId, documentId));

        SetupDefaultChunkerResult(documentId, CreateSamplePreprocessResult(jobId, documentId));

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "chunk-results",
                It.IsAny<string>(),
                It.IsAny<ChunkResult>(),
                cts.Token))
            .ReturnsAsync("chunk-results/blob.json");

        // Act
        await _activity.ExecuteAsync(context, cts.Token);

        // Assert
        _mockStorageService.Verify(
            x => x.DownloadJsonAsync<PreprocessResult>(
                "preprocess-results",
                preprocessBlobPath,
                cts.Token),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static StageContext CreateStageContext(
        Guid jobId,
        Guid documentId,
        string correlationId,
        string? preprocessBlobPath)
    {
        var metadata = new Dictionary<string, object>();

        if (preprocessBlobPath != null)
        {
            metadata[StageMetadataKeys.PreprocessBlobPath] = preprocessBlobPath;
        }

        return new StageContext(
            new ProcessJobModel(jobId, documentId, "idem-key", ProcessJobStatus.Processing, ProcessJobStage.Chunk),
            metadata,
            correlationId);
    }

    private static PreprocessResult CreateSamplePreprocessResult(Guid jobId, Guid documentId)
    {
        return new PreprocessResult
        {
            DocumentId = documentId,
            JobId = jobId,
            Pages =
            [
                new PreprocessedPage
                {
                    PageNumber = 1,
                    NormalizedText = "Sample normalized text for chunking.",
                    TextBlocks = [],
                    Language = "en",
                    WordCount = 6
                }
            ],
            Tables = [],
            FormFields = [],
            Metadata = new PreprocessMetadata
            {
                ProcessedAt = DateTimeOffset.UtcNow,
                ProcessingDuration = TimeSpan.FromSeconds(2),
                PageCount = 1,
                TotalWordCount = 6,
                TotalTables = 0,
                TotalFormFields = 0,
                PrimaryLanguage = "en",
                NormalizationSettings = new Dictionary<string, bool>
                {
                    ["UnicodeNormalization"] = true,
                    ["WhitespaceCleanup"] = true,
                    ["TableConversion"] = true
                },
                Warnings = []
            }
        };
    }

    private void SetupDefaultChunkerResult(Guid documentId, PreprocessResult preprocessResult)
    {
        var chunks = new List<DocumentChunk>
        {
            CreateDocumentChunk(documentId, 0, ChunkType.Text)
        };

        var metadata = new ChunkMetadata
        {
            TotalChunks = 1,
            TextChunks = 1,
            TableChunks = 0,
            FormFieldChunks = 0,
            TotalTokens = 100,
            MaxChunkSize = _options.MaxChunkSize,
            OverlapTokens = _options.OverlapTokens
        };

        _mockDocumentChunker
            .Setup(x => x.ChunkDocument(preprocessResult, It.IsAny<ChunkingOptions>()))
            .Returns((chunks, metadata));
    }

    private static DocumentChunk CreateDocumentChunk(Guid documentId, int index, ChunkType chunkType)
    {
        return new DocumentChunk
        {
            ChunkId = $"chunk-{documentId}-{index}",
            ChunkIndex = index,
            DocumentId = documentId,
            PageNumbers = [1],
            StartOffset = index * 100,
            EndOffset = (index * 100) + 99,
            TokenCount = 100,
            ChunkType = chunkType,
            Content = $"Sample chunk content for index {index}.",
            SourceBlocks = [index]
        };
    }

    #endregion
}
