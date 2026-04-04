using System.Diagnostics;
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Embedding;
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
        IOptions<AzureSearchOptions> options,
        ILogger<AzureSearchVectorStoreService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException(
                "Azure AI Search endpoint must be configured. " +
                "Set AzureSearch:Endpoint in configuration.");
        }

        var endpoint = new Uri(_options.Endpoint);
        var credential = new DefaultAzureCredential();

        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, _options.IndexName, credential);
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

    #endregion
}
