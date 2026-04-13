using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Domain.Entities;
using DocProcessing.Infrastructure.Options;
using DocProcessing.Infrastructure.Services.VectorStore;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Moq;

namespace Infrastructure.Tests.Services.VectorStore;

public sealed class AzureSearchVectorStoreSearchTests
{
    private readonly Mock<SearchIndexClient> _mockIndexClient;
    private readonly Mock<SearchClient> _mockSearchClient;
    private readonly FakeLogger<AzureSearchVectorStoreService> _fakeLogger;
    private readonly IOptions<AzureSearchOptions> _options;
    private readonly AzureSearchVectorStoreService _sut;

    public AzureSearchVectorStoreSearchTests()
    {
        _mockIndexClient = new Mock<SearchIndexClient>();
        _mockSearchClient = new Mock<SearchClient>();
        _fakeLogger = new FakeLogger<AzureSearchVectorStoreService>();
        _options = Options.Create(new AzureSearchOptions
        {
            Endpoint = "https://test.search.windows.net",
            IndexName = "test-index",
            Dimensions = 1536
        });

        _sut = new AzureSearchVectorStoreService(
            _mockIndexClient.Object,
            _mockSearchClient.Object,
            _options,
            _fakeLogger);
    }

    #region Helpers

    private static SearchResults<SearchDocument> BuildSearchResults(
        IEnumerable<SearchResult<SearchDocument>> results,
        long totalCount = 1)
    {
        return SearchModelFactory.SearchResults(
            results,
            totalCount: totalCount,
            facets: null,
            coverage: null,
            rawResponse: new Mock<Response>().Object);
    }

    private static SearchResult<SearchDocument> BuildSearchResult(
        SearchDocument doc,
        double score = 0.667)
    {
        return SearchModelFactory.SearchResult(doc, score: score, highlights: null);
    }

    private static SearchDocument BuildSearchDocument(
        Guid documentId,
        string chunkId = "chunk-1",
        long chunkIndex = 0L,
        string content = "test content",
        string chunkType = "Text",
        long tokenCount = 50L,
        object[]? pageNumbers = null)
    {
        return new SearchDocument(new Dictionary<string, object>
        {
            ["chunkId"] = chunkId,
            ["documentId"] = documentId.ToString(),
            ["chunkIndex"] = chunkIndex,
            ["content"] = content,
            ["chunkType"] = chunkType,
            ["pageNumbers"] = pageNumbers ?? new object[] { 1L, 2L },
            ["tokenCount"] = tokenCount
        });
    }

    private void SetupSearchClientResponse(SearchResults<SearchDocument> searchResults)
    {
        _mockSearchClient
            .Setup(x => x.SearchAsync<SearchDocument>(
                It.IsAny<string?>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(searchResults, new Mock<Response>().Object));
    }

    #endregion

    #region Vector query construction

    [Test]
    public async Task SearchAsync_WithValidEmbedding_CallsSearchClientWithVectorizedQuery()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f, 0.3f];

        var searchResults = BuildSearchResults([], totalCount: 0);
        SetupSearchClientResponse(searchResults);

        // Act
        await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert
        _mockSearchClient.Verify(
            x => x.SearchAsync<SearchDocument>(
                It.IsAny<string?>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task SearchAsync_SetsCorrectTopKOnSearchOptions()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f, 0.3f];
        const int topK = 7;

        SearchOptions? capturedOptions = null;
        _mockSearchClient
            .Setup(x => x.SearchAsync<SearchDocument>(
                It.IsAny<string?>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, SearchOptions, CancellationToken>((_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Response.FromValue(
                BuildSearchResults([], totalCount: 0),
                new Mock<Response>().Object));

        // Act
        await _sut.SearchAsync(embedding, documentId, topK);

        // Assert
        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.Size).IsEqualTo(topK);
    }

    #endregion

    #region OData filter construction

    [Test]
    public async Task SearchAsync_WithDocumentIdFilter_AddsODataFilter()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        SearchOptions? capturedOptions = null;
        _mockSearchClient
            .Setup(x => x.SearchAsync<SearchDocument>(
                It.IsAny<string?>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, SearchOptions, CancellationToken>((_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Response.FromValue(
                BuildSearchResults([], totalCount: 0),
                new Mock<Response>().Object));

        // Act
        await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert
        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.Filter).Contains(documentId.ToString());
        await Assert.That(capturedOptions.Filter).Contains("documentId eq");
    }

    [Test]
    public async Task SearchAsync_WithChunkTypeFilter_AddsChunkTypeToFilter()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        SearchOptions? capturedOptions = null;
        _mockSearchClient
            .Setup(x => x.SearchAsync<SearchDocument>(
                It.IsAny<string?>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, SearchOptions, CancellationToken>((_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Response.FromValue(
                BuildSearchResults([], totalCount: 0),
                new Mock<Response>().Object));

        // Act
        await _sut.SearchAsync(embedding, documentId, topK: 5, chunkTypeFilter: [ChunkType.Table]);

        // Assert
        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.Filter).Contains("chunkType eq 'Table'");
    }

    #endregion

    #region Result mapping

    [Test]
    public async Task SearchAsync_MapsSearchResultToRetrievedChunk()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        var doc = BuildSearchDocument(
            documentId,
            chunkId: "chunk-42",
            chunkIndex: 3L,
            content: "Hello, world!",
            chunkType: "Table",
            tokenCount: 99L,
            pageNumbers: [2L, 3L]);

        var searchResult = BuildSearchResult(doc, score: 1.0);
        var searchResults = BuildSearchResults([searchResult]);
        SetupSearchClientResponse(searchResults);

        // Act
        IReadOnlyList<RetrievedChunk> results = await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert
        await Assert.That(results.Count).IsEqualTo(1);

        RetrievedChunk chunk = results[0];
        await Assert.That(chunk.ChunkId).IsEqualTo("chunk-42");
        await Assert.That(chunk.DocumentId).IsEqualTo(documentId);
        await Assert.That(chunk.ChunkIndex).IsEqualTo(3);
        await Assert.That(chunk.Content).IsEqualTo("Hello, world!");
        await Assert.That(chunk.ChunkType).IsEqualTo(ChunkType.Table);
        await Assert.That(chunk.TokenCount).IsEqualTo(99);
        await Assert.That(chunk.PageNumbers.Count).IsEqualTo(2);
        await Assert.That(chunk.PageNumbers[0]).IsEqualTo(2);
        await Assert.That(chunk.PageNumbers[1]).IsEqualTo(3);
    }

    [Test]
    public async Task SearchAsync_MapsScore_FromSearchResult()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        // Azure score 0.667 → cosine_similarity = 2 - (1/0.667) ≈ 0.5
        const double azureScore = 0.667;
        var doc = BuildSearchDocument(documentId);
        var searchResult = BuildSearchResult(doc, score: azureScore);
        var searchResults = BuildSearchResults([searchResult]);
        SetupSearchClientResponse(searchResults);

        // Act
        IReadOnlyList<RetrievedChunk> results = await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert — score is NORMALIZED, not raw Azure score
        double expectedScore = AzureSearchVectorStoreService.NormalizeScore(azureScore);
        await Assert.That(results[0].Score).IsEqualTo(expectedScore);
    }

    [Test]
    public async Task SearchAsync_WhenSearchReturnsEmpty_ReturnsEmptyList()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        var searchResults = BuildSearchResults([], totalCount: 0);
        SetupSearchClientResponse(searchResults);

        // Act
        IReadOnlyList<RetrievedChunk> results = await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert
        await Assert.That(results.Count).IsEqualTo(0);
    }

    #endregion

    #region Cancellation

    [Test]
    public async Task SearchAsync_PassesCancellationTokenToSearchClient()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];
        using var cts = new CancellationTokenSource();
        CancellationToken expectedToken = cts.Token;

        CancellationToken capturedToken = default;
        _mockSearchClient
            .Setup(x => x.SearchAsync<SearchDocument>(
                It.IsAny<string?>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, SearchOptions, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(Response.FromValue(
                BuildSearchResults([], totalCount: 0),
                new Mock<Response>().Object));

        // Act
        await _sut.SearchAsync(embedding, documentId, topK: 5, cancellationToken: expectedToken);

        // Assert
        await Assert.That(capturedToken).IsEqualTo(expectedToken);
    }

    #endregion

    #region Score normalization (SearchAsync)

    [Test]
    public async Task SearchAsync_NormalizesAzureScoreToCosineSimilarity()
    {
        // Arrange
        // Azure score 0.667 ≈ 1/(1+0.5), meaning cosine_distance ≈ 0.5, so cosine_similarity ≈ 0.5
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        const double azureScore = 0.667;
        var doc = BuildSearchDocument(documentId);
        var searchResult = BuildSearchResult(doc, score: azureScore);
        var searchResults = BuildSearchResults([searchResult]);
        SetupSearchClientResponse(searchResults);

        // Act
        IReadOnlyList<RetrievedChunk> results = await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert — normalized score should be approximately 0.5, not the raw 0.667
        await Assert.That(results[0].Score).IsNotEqualTo(azureScore);
        await Assert.That(results[0].Score).IsGreaterThanOrEqualTo(0.0);
        await Assert.That(results[0].Score).IsLessThanOrEqualTo(1.0);
        // 2 - (1/0.667) ≈ 0.4993, roughly 0.5
        await Assert.That(results[0].Score).IsGreaterThan(0.49);
        await Assert.That(results[0].Score).IsLessThan(0.51);
    }

    [Test]
    public async Task SearchAsync_WithNullScore_ReturnsZeroScore()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        var doc = BuildSearchDocument(documentId);
        // null score
        var searchResult = SearchModelFactory.SearchResult(doc, score: null, highlights: null);
        var searchResults = BuildSearchResults([searchResult]);
        SetupSearchClientResponse(searchResults);

        // Act
        IReadOnlyList<RetrievedChunk> results = await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert
        await Assert.That(results[0].Score).IsEqualTo(0.0);
    }

    [Test]
    public async Task SearchAsync_DoesNotCallEnsureIndexExists()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        float[] embedding = [0.1f, 0.2f];

        var searchResults = BuildSearchResults([], totalCount: 0);
        SetupSearchClientResponse(searchResults);

        // Act
        await _sut.SearchAsync(embedding, documentId, topK: 5);

        // Assert — CreateOrUpdateIndexAsync must NOT be called during SearchAsync
        _mockIndexClient.Verify(
            x => x.CreateOrUpdateIndexAsync(
                It.IsAny<Azure.Search.Documents.Indexes.Models.SearchIndex>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region NormalizeScore (static method)

    [Test]
    public async Task NormalizeScore_WithPerfectMatch_ReturnsOne()
    {
        // Arrange — Azure score 1.0 means cosine_distance=0, cosine_similarity=1
        const double azureScore = 1.0;

        // Act
        double result = AzureSearchVectorStoreService.NormalizeScore(azureScore);

        // Assert
        await Assert.That(result).IsEqualTo(1.0);
    }

    [Test]
    public async Task NormalizeScore_WithZeroSimilarity_ReturnsZero()
    {
        // Arrange — Azure score 0.5 means cosine_distance=1, cosine_similarity=0
        // score = 1/(1+1) = 0.5 → 2 - (1/0.5) = 2 - 2 = 0
        const double azureScore = 0.5;

        // Act
        double result = AzureSearchVectorStoreService.NormalizeScore(azureScore);

        // Assert
        await Assert.That(result).IsEqualTo(0.0);
    }

    [Test]
    public async Task NormalizeScore_WithNull_ReturnsZero()
    {
        // Act
        double result = AzureSearchVectorStoreService.NormalizeScore(null);

        // Assert
        await Assert.That(result).IsEqualTo(0.0);
    }

    [Test]
    public async Task NormalizeScore_WithNegative_ReturnsZero()
    {
        // Arrange — negative score is invalid; clamp to 0
        const double azureScore = -1.0;

        // Act
        double result = AzureSearchVectorStoreService.NormalizeScore(azureScore);

        // Assert
        await Assert.That(result).IsEqualTo(0.0);
    }

    #endregion
}
