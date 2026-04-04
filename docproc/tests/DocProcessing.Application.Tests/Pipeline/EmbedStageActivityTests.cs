using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models;
using DocProcessing.Application.Models.Chunking;
using DocProcessing.Application.Models.Embedding;
using DocProcessing.Application.Pipeline;
using DocProcessing.Application.Pipeline.Options;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DocProcessing.Application.Tests.Pipeline;

public sealed class EmbedStageActivityTests
{
    private readonly FakeLogger<EmbedStageActivity> _logger;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IVectorStoreService> _mockVectorStoreService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly ChunkingOptions _chunkingOptions;
    private readonly EmbedStageActivity _activity;

    public EmbedStageActivityTests()
    {
        _logger = new FakeLogger<EmbedStageActivity>();
        _mockStorageService = new Mock<IStorageService>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockVectorStoreService = new Mock<IVectorStoreService>();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero));

        _embeddingOptions = new EmbeddingOptions
        {
            DeploymentName = "text-embedding-3-small",
            Dimensions = 1536,
            BatchSize = 100,
            OutputBlobContainer = "embed-results"
        };

        _chunkingOptions = new ChunkingOptions
        {
            OutputBlobContainer = "chunk-results",
            MaxChunkSize = 512,
            OverlapTokens = 50,
            TokenEstimationFactor = 1.3
        };

        _activity = new EmbedStageActivity(
            _logger,
            _mockStorageService.Object,
            _mockEmbeddingService.Object,
            _mockVectorStoreService.Object,
            Options.Create(_embeddingOptions),
            Options.Create(_chunkingOptions),
            _timeProvider);
    }

    #region Property and Guard Tests

    [Test]
    public async Task StageName_ReturnsEmbed()
    {
        await Assert.That(_activity.StageName).IsEqualTo("Embed");
    }

    [Test]
    public async Task Stage_ReturnsProcessJobStageEmbed()
    {
        await Assert.That(_activity.Stage).IsEqualTo(ProcessJobStage.Embed);
    }

    [Test]
    public async Task ExecuteAsync_NullContext_ThrowsArgumentNullException()
    {
        await Assert.That(async () => { await _activity.ExecuteAsync(null!); }).ThrowsExactly<ArgumentNullException>();
    }

    #endregion

    #region Input Validation Tests

    [Test]
    public async Task ExecuteAsync_WhenChunkBlobPathMissing_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var context = CreateStageContext(jobId, documentId, "corr-001", chunkBlobPath: null);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("EMBED_MISSING_CHUNK_PATH");
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_WhenChunkBlobPathNotString_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            [StageMetadataKeys.ChunkBlobPath] = 42  // non-string value
        };
        var context = new StageContext(
            new ProcessJobModel(jobId, documentId, "idem-key", ProcessJobStatus.Processing, ProcessJobStage.Embed),
            metadata,
            "corr-002");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("EMBED_MISSING_CHUNK_PATH");
    }

    [Test]
    public async Task ExecuteAsync_WhenChunkResultNotFoundInBlob_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-003", chunkBlobPath);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<ChunkResult>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChunkResult?)null);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("EMBED_CHUNK_RESULT_NOT_FOUND");
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    #endregion

    #region Happy Path Tests

    [Test]
    public async Task ExecuteAsync_WithValidInput_GeneratesEmbeddingsAndReturnsSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-004", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 2);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess("embed-results/tenant1/doc1/embed-result.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_SetsCorrectMetadataKeys()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var expectedEmbedBlobPath = "embed-results/tenant1/doc1/embed-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-005", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 2);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess(expectedEmbedBlobPath);

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.EmbedBlobPath)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.EmbedBlobPath]).IsEqualTo(expectedEmbedBlobPath);
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.EmbeddedChunks)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.EmbeddedChunks]).IsEqualTo(2);
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.EmbeddingModel)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.EmbeddingModel]).IsEqualTo(_embeddingOptions.DeploymentName);
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.EmbeddingDimensions)).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.EmbeddingDimensions]).IsEqualTo(_embeddingOptions.Dimensions);
        await Assert.That(result.Metadata.ContainsKey(StageMetadataKeys.ProcessingDurationMs)).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_UploadsEmbedResultToBlob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-006", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 1);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess("embed-results/blob.json");

        // Act
        await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        _mockStorageService.Verify(
            x => x.UploadJsonAsync(
                "embed-results",
                It.IsAny<string>(),
                It.IsAny<EmbedResult>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithValidInput_UpsertsBatchesToVectorStore()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-007", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 3);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess("embed-results/blob.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockVectorStoreService.Verify(
            x => x.UpsertChunksAsync(
                It.IsAny<IReadOnlyList<EmbeddedChunk>>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Batching Tests

    [Test]
    public async Task ExecuteAsync_BatchesChunksCorrectly_WhenMoreThanBatchSize()
    {
        // Arrange — BatchSize=2, 5 chunks → 3 calls (2+2+1)
        var batchingOptions = new EmbeddingOptions
        {
            DeploymentName = "text-embedding-3-small",
            Dimensions = 1536,
            BatchSize = 2,
            OutputBlobContainer = "embed-results"
        };
        var activity = new EmbedStageActivity(
            _logger,
            _mockStorageService.Object,
            _mockEmbeddingService.Object,
            _mockVectorStoreService.Object,
            Options.Create(batchingOptions),
            Options.Create(_chunkingOptions),
            _timeProvider);

        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-batch-001", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 5);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken _) =>
                (IReadOnlyList<float[]>)texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToList());

        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess("embed-results/blob.json");

        // Act
        var result = await activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockEmbeddingService.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Test]
    public async Task ExecuteAsync_WithSingleChunk_DoesNotBatch()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-single-001", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 1);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess("embed-results/blob.json");

        // Act
        await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        _mockEmbeddingService.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(1));
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task ExecuteAsync_WithEmptyChunkList_ReturnsSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-empty-001", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 0);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupBlobUploadSuccess("embed-results/blob.json");

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Metadata[StageMetadataKeys.EmbeddedChunks]).IsEqualTo(0);

        _mockEmbeddingService.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ExecuteAsync_WhenEmbeddingServiceThrows_ReturnsFailureWithEmbedError()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-err-001", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 2);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service unavailable"));

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("EMBED_ERROR");
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_WhenVectorStoreThrows_ReturnsFailureWithEmbedError()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-err-002", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 2);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);

        _mockVectorStoreService
            .Setup(x => x.UpsertChunksAsync(It.IsAny<IReadOnlyList<EmbeddedChunk>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vector store write failed"));

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("EMBED_ERROR");
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task ExecuteAsync_WhenBlobUploadFails_ReturnsFailureWithUploadError()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-err-003", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 2);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "embed-results",
                It.IsAny<string>(),
                It.IsAny<EmbedResult>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob storage upload failed"));

        // Act
        var result = await _activity.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo("EMBED_UPLOAD_ERROR");
        await Assert.That(result.ErrorMessage).IsNotNull();
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
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, correlationId, chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 1);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess("embed-results/blob.json");

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
    public async Task ExecuteAsync_OnSuccess_LogsCompletionWithStats()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-log-complete-001", chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 2);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);
        SetupEmbeddingServiceForChunks(chunkResult.Chunks);
        SetupVectorStoreSuccess();
        SetupBlobUploadSuccess("embed-results/blob.json");

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
    public async Task ExecuteAsync_OnFailure_LogsErrorWithJobIdAndCorrelationId()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var correlationId = "corr-log-error-001";
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, correlationId, chunkBlobPath);
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 1);

        SetupDownloadChunkResult(chunkBlobPath, chunkResult);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding failure"));

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
    public async Task ExecuteAsync_WithCancellationToken_PassesToServices()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkBlobPath = "chunk-results/tenant1/doc1/chunk-result.json";
        var context = CreateStageContext(jobId, documentId, "corr-cancel-001", chunkBlobPath);
        var cts = new CancellationTokenSource();
        var chunkResult = CreateSampleChunkResult(jobId, documentId, chunkCount: 1);

        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<ChunkResult>(
                "chunk-results",
                chunkBlobPath,
                cts.Token))
            .ReturnsAsync(chunkResult);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), cts.Token))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken _) =>
                (IReadOnlyList<float[]>)texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToList());

        _mockVectorStoreService
            .Setup(x => x.UpsertChunksAsync(It.IsAny<IReadOnlyList<EmbeddedChunk>>(), cts.Token))
            .Returns(Task.CompletedTask);

        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "embed-results",
                It.IsAny<string>(),
                It.IsAny<EmbedResult>(),
                cts.Token))
            .ReturnsAsync("embed-results/blob.json");

        // Act
        await _activity.ExecuteAsync(context, cts.Token);

        // Assert
        _mockStorageService.Verify(
            x => x.DownloadJsonAsync<ChunkResult>(
                "chunk-results",
                chunkBlobPath,
                cts.Token),
            Times.Once);

        _mockEmbeddingService.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), cts.Token),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static StageContext CreateStageContext(
        Guid jobId,
        Guid documentId,
        string correlationId,
        string? chunkBlobPath)
    {
        var metadata = new Dictionary<string, object>();

        if (chunkBlobPath != null)
        {
            metadata[StageMetadataKeys.ChunkBlobPath] = chunkBlobPath;
        }

        return new StageContext(
            new ProcessJobModel(jobId, documentId, "idem-key", ProcessJobStatus.Processing, ProcessJobStage.Embed),
            metadata,
            correlationId);
    }

    private static ChunkResult CreateSampleChunkResult(Guid jobId, Guid documentId, int chunkCount)
    {
        var chunks = Enumerable.Range(0, chunkCount)
            .Select(i => CreateDocumentChunk(documentId, i))
            .ToList();

        return new ChunkResult
        {
            DocumentId = documentId,
            JobId = jobId,
            Chunks = chunks,
            Metadata = new ChunkMetadata
            {
                TotalChunks = chunkCount,
                TextChunks = chunkCount,
                TableChunks = 0,
                FormFieldChunks = 0,
                TotalTokens = chunkCount * 100,
                MaxChunkSize = 512,
                OverlapTokens = 50
            },
            ProcessedAt = DateTimeOffset.UtcNow,
            ProcessingDuration = TimeSpan.FromSeconds(1)
        };
    }

    private static DocumentChunk CreateDocumentChunk(Guid documentId, int index)
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
            ChunkType = ChunkType.Text,
            Content = $"Sample chunk content for index {index}.",
            SourceBlocks = [index]
        };
    }

    private void SetupDownloadChunkResult(string chunkBlobPath, ChunkResult chunkResult)
    {
        _mockStorageService
            .Setup(x => x.DownloadJsonAsync<ChunkResult>(
                "chunk-results",
                chunkBlobPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunkResult);
    }

    private void SetupEmbeddingServiceForChunks(IReadOnlyList<DocumentChunk> chunks)
    {
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken _) =>
                (IReadOnlyList<float[]>)texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToList());
    }

    private void SetupVectorStoreSuccess()
    {
        _mockVectorStoreService
            .Setup(x => x.UpsertChunksAsync(It.IsAny<IReadOnlyList<EmbeddedChunk>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupBlobUploadSuccess(string returnedBlobPath)
    {
        _mockStorageService
            .Setup(x => x.UploadJsonAsync(
                "embed-results",
                It.IsAny<string>(),
                It.IsAny<EmbedResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnedBlobPath);
    }

    #endregion
}
