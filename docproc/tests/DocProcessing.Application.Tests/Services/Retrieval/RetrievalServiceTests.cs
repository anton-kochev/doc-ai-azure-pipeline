using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Application.Pipeline.Options;
using DocProcessing.Application.Services;
using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Exceptions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DocProcessing.Application.Tests.Services.Retrieval;

public sealed class RetrievalServiceTests
{
    private readonly FakeLogger<RetrievalService> _logger;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IVectorStoreService> _mockVectorStoreService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RetrievalOptions _options;
    private readonly RetrievalService _sut;

    private static readonly float[] DefaultEmbedding = [0.1f, 0.2f, 0.3f];

    public RetrievalServiceTests()
    {
        _logger = new FakeLogger<RetrievalService>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockVectorStoreService = new Mock<IVectorStoreService>();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _options = new RetrievalOptions
        {
            DefaultTopK = 10,
            DefaultScoreThreshold = 0.3,
            MaxTopK = 50
        };

        SetupEmbeddingService(DefaultEmbedding);
        SetupVectorStoreSearch(
        [
            CreateRetrievedChunk("chunk-1", score: 0.9),
            CreateRetrievedChunk("chunk-2", score: 0.8)
        ]);

        _sut = new RetrievalService(
            _mockEmbeddingService.Object,
            _mockVectorStoreService.Object,
            Options.Create(_options),
            _logger,
            _timeProvider);
    }

    #region Input Validation

    [Test]
    public async Task RetrieveAsync_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task RetrieveAsync_WithEmptyQueryText_ThrowsArgumentException()
    {
        // Arrange
        var query = new RetrievalQuery
        {
            QueryText = string.Empty,
            DocumentId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task RetrieveAsync_WithWhitespaceOnlyQueryText_ThrowsArgumentException()
    {
        // Arrange
        var query = new RetrievalQuery
        {
            QueryText = "   ",
            DocumentId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task RetrieveAsync_WithEmptyDocumentId_ThrowsArgumentException()
    {
        // Arrange
        var query = new RetrievalQuery
        {
            QueryText = "What is the invoice total?",
            DocumentId = Guid.Empty
        };

        // Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<ArgumentException>();
    }

    #endregion

    #region TopK Defaults and Clamping

    [Test]
    public async Task RetrieveAsync_WithNullTopK_UsesDefaultTopK()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        int? capturedTopK = null;

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                documentId,
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<float[], Guid, int, IReadOnlyList<ChunkType>?, CancellationToken>(
                (_, _, topK, _, _) => capturedTopK = topK)
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            TopK = null
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(capturedTopK).IsEqualTo(_options.DefaultTopK);
    }

    [Test]
    public async Task RetrieveAsync_WithTopKExceedingMax_ClampsToMaxTopK()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        int? capturedTopK = null;

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                documentId,
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<float[], Guid, int, IReadOnlyList<ChunkType>?, CancellationToken>(
                (_, _, topK, _, _) => capturedTopK = topK)
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            TopK = 9999
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(capturedTopK).IsEqualTo(_options.MaxTopK);
    }

    [Test]
    public async Task RetrieveAsync_WithNegativeTopK_ThrowsArgumentException()
    {
        // Arrange
        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = Guid.NewGuid(),
            TopK = -1
        };

        // Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<ArgumentException>();
    }

    #endregion

    #region Score Threshold and Filtering

    [Test]
    public async Task RetrieveAsync_WithNullScoreThreshold_UsesDefaultThreshold()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-above", score: _options.DefaultScoreThreshold + 0.1),
            CreateRetrievedChunk("chunk-below", score: _options.DefaultScoreThreshold - 0.1)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ScoreThreshold = null
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert — only the chunk above the default threshold should pass
        await Assert.That(result.Chunks.Count).IsEqualTo(1);
        await Assert.That(result.Chunks[0].ChunkId).IsEqualTo("chunk-above");
    }

    [Test]
    public async Task RetrieveAsync_WithCustomScoreThreshold_FiltersChunksBelowThreshold()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        double customThreshold = 0.7;
        var chunks = new[]
        {
            CreateRetrievedChunk("high-score", score: 0.9),
            CreateRetrievedChunk("mid-score", score: 0.6),
            CreateRetrievedChunk("low-score", score: 0.3)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ScoreThreshold = customThreshold
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert — only chunk with score >= 0.7 passes
        await Assert.That(result.Chunks.Count).IsEqualTo(1);
        await Assert.That(result.Chunks[0].ChunkId).IsEqualTo("high-score");
    }

    [Test]
    public async Task RetrieveAsync_WithThresholdZero_ReturnsAllChunks()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-1", score: 0.9),
            CreateRetrievedChunk("chunk-2", score: 0.5),
            CreateRetrievedChunk("chunk-3", score: 0.1)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ScoreThreshold = 0.0
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result.Chunks.Count).IsEqualTo(3);
    }

    [Test]
    public async Task RetrieveAsync_WhenAllChunksBelowThreshold_ReturnsEmptyResult()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-1", score: 0.1),
            CreateRetrievedChunk("chunk-2", score: 0.2)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ScoreThreshold = 0.9
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result.Chunks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RetrieveAsync_WithChunkAtExactThreshold_IncludesChunk()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        double threshold = 0.3;
        var chunks = new[]
        {
            CreateRetrievedChunk("exact-threshold", score: 0.3),
            CreateRetrievedChunk("below-threshold", score: 0.29)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ScoreThreshold = threshold
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert — score >= threshold, so the exact-threshold chunk is included
        await Assert.That(result.Chunks.Count).IsEqualTo(1);
        await Assert.That(result.Chunks[0].ChunkId).IsEqualTo("exact-threshold");
    }

    #endregion

    #region Happy Path and Passthrough

    [Test]
    public async Task RetrieveAsync_WithValidQuery_ReturnsRetrievalResult()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var queryText = "What is the invoice total?";
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-1", score: 0.9),
            CreateRetrievedChunk("chunk-2", score: 0.8)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = queryText,
            DocumentId = documentId
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.QueryText).IsEqualTo(queryText);
        await Assert.That(result.DocumentId).IsEqualTo(documentId);
        await Assert.That(result.Chunks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RetrieveAsync_WithValidQuery_EmbedsQueryTextAsSingleItemList()
    {
        // Arrange
        var queryText = "What is the invoice date?";
        IReadOnlyList<string>? capturedTexts = null;

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, CancellationToken>((texts, _) => capturedTexts = texts)
            .ReturnsAsync(new[] { DefaultEmbedding });

        var query = new RetrievalQuery
        {
            QueryText = queryText,
            DocumentId = Guid.NewGuid()
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert — must be a single-item list containing exactly the query text
        await Assert.That(capturedTexts).IsNotNull();
        await Assert.That(capturedTexts!.Count).IsEqualTo(1);
        await Assert.That(capturedTexts[0]).IsEqualTo(queryText);
    }

    [Test]
    public async Task RetrieveAsync_WithValidQuery_PassesEmbeddingToVectorStoreSearch()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var embedding = new float[] { 0.5f, 0.6f, 0.7f };
        float[]? capturedEmbedding = null;

        SetupEmbeddingService(embedding);

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                documentId,
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<float[], Guid, int, IReadOnlyList<ChunkType>?, CancellationToken>(
                (emb, _, _, _, _) => capturedEmbedding = emb)
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(capturedEmbedding).IsNotNull();
        await Assert.That(capturedEmbedding![0]).IsEqualTo(embedding[0]);
        await Assert.That(capturedEmbedding[1]).IsEqualTo(embedding[1]);
        await Assert.That(capturedEmbedding[2]).IsEqualTo(embedding[2]);
    }

    [Test]
    public async Task RetrieveAsync_WithChunkTypeFilter_PassesFilterToVectorStoreSearch()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var chunkTypeFilter = new[] { ChunkType.Table };
        IReadOnlyList<ChunkType>? capturedFilter = null;

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                documentId,
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<float[], Guid, int, IReadOnlyList<ChunkType>?, CancellationToken>(
                (_, _, _, filter, _) => capturedFilter = filter)
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ChunkTypeFilter = chunkTypeFilter
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(capturedFilter).IsNotNull();
        await Assert.That(capturedFilter!.Count).IsEqualTo(1);
        await Assert.That(capturedFilter[0]).IsEqualTo(ChunkType.Table);
    }

    [Test]
    public async Task RetrieveAsync_WithNullChunkTypeFilter_PassesNullToVectorStoreSearch()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        IReadOnlyList<ChunkType>? capturedFilter = new List<ChunkType> { ChunkType.Text }; // sentinel, non-null

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                documentId,
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<float[], Guid, int, IReadOnlyList<ChunkType>?, CancellationToken>(
                (_, _, _, filter, _) => capturedFilter = filter)
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ChunkTypeFilter = null
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(capturedFilter).IsNull();
    }

    [Test]
    public async Task RetrieveAsync_WhenThresholdFiltersResults_ReturnFewerThanTopK()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Vector store returns 5 chunks, threshold filters down to 3
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-1", score: 0.9),
            CreateRetrievedChunk("chunk-2", score: 0.8),
            CreateRetrievedChunk("chunk-3", score: 0.7),
            CreateRetrievedChunk("chunk-4", score: 0.2),
            CreateRetrievedChunk("chunk-5", score: 0.1)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            TopK = 5,
            ScoreThreshold = 0.5
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert — returned chunks are fewer than topK because of threshold
        await Assert.That(result.Chunks.Count).IsEqualTo(3);
    }

    #endregion

    #region Result Metadata

    [Test]
    public async Task RetrieveAsync_SetsTotalCandidatesToPreFilterCount()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-1", score: 0.9),
            CreateRetrievedChunk("chunk-2", score: 0.8),
            CreateRetrievedChunk("chunk-3", score: 0.7),
            CreateRetrievedChunk("chunk-4", score: 0.2),
            CreateRetrievedChunk("chunk-5", score: 0.1)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ScoreThreshold = 0.5
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert — TotalCandidates is the raw count before filtering
        await Assert.That(result.TotalCandidates).IsEqualTo(5);
        await Assert.That(result.Chunks.Count).IsEqualTo(3);
    }

    [Test]
    public async Task RetrieveAsync_ComputesTotalTokensFromReturnedChunks()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-1", score: 0.9, tokenCount: 100),
            CreateRetrievedChunk("chunk-2", score: 0.8, tokenCount: 150)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId,
            ScoreThreshold = 0.0
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result.TotalTokens).IsEqualTo(250);
    }

    [Test]
    public async Task RetrieveAsync_RecordsSearchDuration()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var expectedSearchDuration = TimeSpan.FromMilliseconds(200);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { DefaultEmbedding });

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                _timeProvider.Advance(expectedSearchDuration);
                return (IReadOnlyList<RetrievedChunk>)Array.Empty<RetrievedChunk>();
            });

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result.SearchDuration).IsEqualTo(expectedSearchDuration);
    }

    [Test]
    public async Task RetrieveAsync_RecordsEmbeddingDuration()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var expectedEmbeddingDuration = TimeSpan.FromMilliseconds(100);

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                _timeProvider.Advance(expectedEmbeddingDuration);
                return (IReadOnlyList<float[]>)new[] { DefaultEmbedding };
            });

        SetupVectorStoreSearch(Array.Empty<RetrievedChunk>(), documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result.EmbeddingDuration).IsEqualTo(expectedEmbeddingDuration);
    }

    #endregion

    #region Empty Results

    [Test]
    public async Task RetrieveAsync_WhenVectorStoreReturnsEmpty_ReturnsEmptyChunksResult()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        SetupVectorStoreSearch(Array.Empty<RetrievedChunk>(), documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result.Chunks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RetrieveAsync_WhenVectorStoreReturnsEmpty_SetsTotalCandidatesToZero()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        SetupVectorStoreSearch(Array.Empty<RetrievedChunk>(), documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        var result = await _sut.RetrieveAsync(query);

        // Assert
        await Assert.That(result.TotalCandidates).IsEqualTo(0);
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task RetrieveAsync_WhenEmbeddingServiceThrows_ThrowsRetrievalFailedException()
    {
        // Arrange
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding model unavailable"));

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<RetrievalFailedException>();
    }

    [Test]
    public async Task RetrieveAsync_WhenEmbeddingServiceThrows_IncludesDocumentIdInException()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding model unavailable"));

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act & Assert
        var exception = await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<RetrievalFailedException>();

        await Assert.That(exception!.DocumentId).IsEqualTo(documentId);
    }

    [Test]
    public async Task RetrieveAsync_WhenVectorStoreThrows_ThrowsRetrievalFailedException()
    {
        // Arrange
        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vector store search failed"));

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<RetrievalFailedException>();
    }

    [Test]
    public async Task RetrieveAsync_WhenVectorStoreThrows_PreservesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Vector store connection timeout");

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(innerException);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = Guid.NewGuid()
        };

        // Act & Assert
        var exception = await Assert.That(async () => await _sut.RetrieveAsync(query))
            .ThrowsExactly<RetrievalFailedException>();

        await Assert.That(exception!.InnerException).IsEqualTo(innerException);
    }

    #endregion

    #region Cancellation

    [Test]
    public async Task RetrieveAsync_PassesCancellationTokenToEmbeddingService()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var documentId = Guid.NewGuid();
        CancellationToken capturedToken = default;

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                cts.Token))
            .Callback<IReadOnlyList<string>, CancellationToken>((_, ct) => capturedToken = ct)
            .ReturnsAsync(new[] { DefaultEmbedding });

        SetupVectorStoreSearch(Array.Empty<RetrievedChunk>(), documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        await _sut.RetrieveAsync(query, cts.Token);

        // Assert
        await Assert.That(capturedToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task RetrieveAsync_PassesCancellationTokenToVectorStoreSearch()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var documentId = Guid.NewGuid();
        CancellationToken capturedToken = default;

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                documentId,
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                cts.Token))
            .Callback<float[], Guid, int, IReadOnlyList<ChunkType>?, CancellationToken>(
                (_, _, _, _, ct) => capturedToken = ct)
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        await _sut.RetrieveAsync(query, cts.Token);

        // Assert
        await Assert.That(capturedToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task RetrieveAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.That(async () => await _sut.RetrieveAsync(query, cts.Token))
            .ThrowsExactly<OperationCanceledException>();
    }

    #endregion

    #region Logging

    [Test]
    public async Task RetrieveAsync_LogsSearchStartWithDocumentIdAndQueryLength()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var queryText = "What is the total amount?";

        var query = new RetrievalQuery
        {
            QueryText = queryText,
            DocumentId = documentId
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs.Any(l =>
            l.Level == Microsoft.Extensions.Logging.LogLevel.Information &&
            l.Message.Contains(documentId.ToString()))).IsTrue();
    }

    [Test]
    public async Task RetrieveAsync_OnSuccess_LogsResultCountAndDuration()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var chunks = new[]
        {
            CreateRetrievedChunk("chunk-1", score: 0.9),
            CreateRetrievedChunk("chunk-2", score: 0.8)
        };

        SetupVectorStoreSearch(chunks, documentId);

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        await _sut.RetrieveAsync(query);

        // Assert — at least one Information log after the call
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs.Any(l =>
            l.Level == Microsoft.Extensions.Logging.LogLevel.Information)).IsTrue();
    }

    [Test]
    public async Task RetrieveAsync_OnFailure_LogsErrorWithDocumentId()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        _mockVectorStoreService
            .Setup(x => x.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ChunkType>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vector store unavailable"));

        var query = new RetrievalQuery
        {
            QueryText = "test query",
            DocumentId = documentId
        };

        // Act
        try
        {
            await _sut.RetrieveAsync(query);
        }
        catch (RetrievalFailedException)
        {
            // expected
        }

        // Assert
        var logs = _logger.Collector.GetSnapshot();
        await Assert.That(logs.Any(l =>
            l.Level == Microsoft.Extensions.Logging.LogLevel.Error &&
            l.Message.Contains(documentId.ToString()))).IsTrue();
    }

    #endregion

    #region Helper Methods

    private static RetrievedChunk CreateRetrievedChunk(
        string chunkId,
        double score,
        int tokenCount = 50,
        ChunkType chunkType = ChunkType.Text,
        Guid? documentId = null)
    {
        return new RetrievedChunk
        {
            ChunkId = chunkId,
            DocumentId = documentId ?? Guid.NewGuid(),
            ChunkIndex = 0,
            Content = $"Content for {chunkId}",
            ChunkType = chunkType,
            TokenCount = tokenCount,
            Score = score
        };
    }

    private void SetupEmbeddingService(float[] embedding)
    {
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { embedding });
    }

    private void SetupVectorStoreSearch(
        IReadOnlyList<RetrievedChunk> results,
        Guid? documentId = null)
    {
        if (documentId.HasValue)
        {
            _mockVectorStoreService
                .Setup(x => x.SearchAsync(
                    It.IsAny<float[]>(),
                    documentId.Value,
                    It.IsAny<int>(),
                    It.IsAny<IReadOnlyList<ChunkType>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(results);
        }
        else
        {
            _mockVectorStoreService
                .Setup(x => x.SearchAsync(
                    It.IsAny<float[]>(),
                    It.IsAny<Guid>(),
                    It.IsAny<int>(),
                    It.IsAny<IReadOnlyList<ChunkType>?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(results);
        }
    }

    #endregion
}
