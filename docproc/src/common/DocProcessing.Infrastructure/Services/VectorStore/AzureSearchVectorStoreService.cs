using System.Diagnostics;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Embedding;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Domain.Entities;
using DocProcessing.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Infrastructure.Services.VectorStore;

/// <summary>
/// Azure AI Search implementation of the vector store service.
/// Stores embedded document chunks in a search index with vector search capability.
/// </summary>
public sealed partial class AzureSearchVectorStoreService : IVectorStoreService
{
    private const string VectorSearchHnswProfile = "default-hnsw-profile";
    private const string VectorSearchHnswConfig = "default-hnsw-config";

    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly AzureSearchOptions _options;
    private readonly ILogger<AzureSearchVectorStoreService> _logger;

    private int _indexEnsured;

    public AzureSearchVectorStoreService(
        SearchIndexClient indexClient,
        SearchClient searchClient,
        IOptions<AzureSearchOptions> options,
        ILogger<AzureSearchVectorStoreService> logger)
    {
        ArgumentNullException.ThrowIfNull(indexClient);
        ArgumentNullException.ThrowIfNull(searchClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _indexClient = indexClient;
        _searchClient = searchClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task UpsertChunksAsync(
        IReadOnlyList<EmbeddedChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        LogUpsertStarted(chunks.Count, _options.IndexName);

        try
        {
            await EnsureIndexExistsAsync(cancellationToken);

            IReadOnlyList<SearchDocument> documents = MapToSearchDocuments(chunks);

            await _searchClient.MergeOrUploadDocumentsAsync(documents, cancellationToken: cancellationToken);

            stopwatch.Stop();
            LogUpsertCompleted(chunks.Count, stopwatch.ElapsedMilliseconds, _options.IndexName);
        }
        catch (RequestFailedException ex)
        {
            LogUpsertFailed(ex, chunks.Count, _options.IndexName);
            throw;
        }
        catch (OperationCanceledException)
        {
            LogUpsertCancelled(chunks.Count, _options.IndexName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        Guid documentId,
        int topK,
        IReadOnlyList<ChunkType>? chunkTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        string filter = $"documentId eq '{documentId}'";
        if (chunkTypeFilter is { Count: > 0 })
        {
            string typeFilter = string.Join(" or ",
                chunkTypeFilter.Select(t => $"chunkType eq '{t}'"));
            filter = $"({filter}) and ({typeFilter})";
        }

        var searchOptions = new SearchOptions
        {
            Filter = filter,
            Size = topK,
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryEmbedding)
                    {
                        KNearestNeighborsCount = topK,
                        Fields = { "embedding" }
                    }
                }
            },
            Select = { "chunkId", "documentId", "chunkIndex", "content",
                       "chunkType", "pageNumbers", "tokenCount" }
        };

        SearchResults<SearchDocument> response = await _searchClient
            .SearchAsync<SearchDocument>(null, searchOptions, cancellationToken);

        var results = new List<RetrievedChunk>();

        await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
        {
            SearchDocument doc = result.Document;
            results.Add(new RetrievedChunk
            {
                ChunkId = doc.GetString("chunkId"),
                DocumentId = Guid.Parse(doc.GetString("documentId")),
                ChunkIndex = (int)(long)doc["chunkIndex"],
                Content = doc.GetString("content"),
                ChunkType = Enum.Parse<ChunkType>(doc.GetString("chunkType")),
                PageNumbers = ((IEnumerable<object>)doc["pageNumbers"])
                    .Select(p => (int)(long)p).ToList(),
                TokenCount = (int)(long)doc["tokenCount"],
                Score = NormalizeScore(result.Score)
            });
        }

        LogSearchCompleted(results.Count, _options.IndexName);

        return results;
    }

    /// <summary>
    /// Converts Azure AI Search's vector score to cosine similarity.
    /// Azure uses the formula: score = 1 / (1 + cosine_distance).
    /// We invert to: cosine_similarity = 1 - cosine_distance = 2 - (1 / score), clamped to [0, 1].
    /// See: https://learn.microsoft.com/azure/search/vector-search-ranking
    /// </summary>
    public static double NormalizeScore(double? azureScore)
    {
        if (azureScore is null or <= 0.0)
        {
            return 0.0;
        }

        double cosineSimilarity = 2.0 - (1.0 / azureScore.Value);
        return Math.Clamp(cosineSimilarity, 0.0, 1.0);
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _indexEnsured) == 1)
        {
            return;
        }

        var index = new SearchIndex(_options.IndexName)
        {
            Fields =
            [
                new SearchableField("chunkId") { IsKey = true, IsFilterable = true },
                new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsSortable = true },
                new SearchableField("content"),
                new SimpleField("chunkType", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("pageNumbers", SearchFieldDataType.Collection(SearchFieldDataType.Int32)) { IsFilterable = true },
                new SimpleField("tokenCount", SearchFieldDataType.Int32) { IsSortable = true },
                new VectorSearchField("embedding", _options.Dimensions, VectorSearchHnswProfile)
            ],
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration(VectorSearchHnswConfig) },
                Profiles = { new VectorSearchProfile(VectorSearchHnswProfile, VectorSearchHnswConfig) }
            }
        };

        await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);

        Volatile.Write(ref _indexEnsured, 1);
        LogIndexEnsured(_options.IndexName);
    }

    private static IReadOnlyList<SearchDocument> MapToSearchDocuments(IReadOnlyList<EmbeddedChunk> chunks)
    {
        var documents = new SearchDocument[chunks.Count];

        for (int i = 0; i < chunks.Count; i++)
        {
            EmbeddedChunk chunk = chunks[i];

            documents[i] = new SearchDocument(new Dictionary<string, object?>
            {
                ["chunkId"] = chunk.ChunkId,
                ["documentId"] = chunk.DocumentId.ToString(),
                ["chunkIndex"] = chunk.ChunkIndex,
                ["content"] = chunk.Content,
                ["chunkType"] = chunk.ChunkType.ToString(),
                ["pageNumbers"] = chunk.PageNumbers.ToArray(),
                ["tokenCount"] = chunk.TokenCount,
                ["embedding"] = chunk.Embedding
            });
        }

        return documents;
    }

    #region Logging

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information,
        Message = "Starting vector store upsert. ChunkCount={ChunkCount}, IndexName={IndexName}")]
    private partial void LogUpsertStarted(int chunkCount, string indexName);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Information,
        Message = "Vector store upsert completed. ChunkCount={ChunkCount}, DurationMs={DurationMs}, IndexName={IndexName}")]
    private partial void LogUpsertCompleted(int chunkCount, long durationMs, string indexName);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Error,
        Message = "Vector store upsert failed. ChunkCount={ChunkCount}, IndexName={IndexName}")]
    private partial void LogUpsertFailed(Exception exception, int chunkCount, string indexName);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Warning,
        Message = "Vector store upsert cancelled. ChunkCount={ChunkCount}, IndexName={IndexName}")]
    private partial void LogUpsertCancelled(int chunkCount, string indexName);

    [LoggerMessage(EventId = 6005, Level = LogLevel.Information,
        Message = "Search index ensured. IndexName={IndexName}")]
    private partial void LogIndexEnsured(string indexName);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Information,
        Message = "Vector search completed. ResultCount={ResultCount}, IndexName={IndexName}")]
    private partial void LogSearchCompleted(int resultCount, string indexName);

    #endregion
}
