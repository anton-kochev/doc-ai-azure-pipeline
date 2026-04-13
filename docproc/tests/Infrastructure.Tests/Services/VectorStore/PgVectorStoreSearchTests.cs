using DocProcessing.Application.Models.Embedding;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Domain.Entities;
using DocProcessing.Infrastructure.Options;
using DocProcessing.Infrastructure.Services.VectorStore;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Npgsql;
namespace Infrastructure.Tests.Services.VectorStore;

/// <summary>
/// Integration tests for <see cref="PgVectorStoreService.SearchAsync"/>.
/// Requires a running pgvector container on localhost:5433.
/// Start with: docker compose up -d (from the docproc/ directory)
/// </summary>
[Category("Integration")]
public sealed class PgVectorStoreSearchTests : IDisposable
{
    // Connection to the pgvector container defined in docker-compose.yml
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=test_vectors;Username=postgres;Password=postgres";

    // Each test class run gets an isolated table to avoid cross-test collisions
    private static readonly string TableName =
        $"test_chunks_{Guid.NewGuid().ToString("N")[..8]}";

    // Use 3-dimensional vectors for simplicity in tests
    private const int Dimensions = 3;

    private static NpgsqlDataSource? _dataSource;
    private static bool _pgvectorAvailable;

    private PgVectorStoreService _sut = null!;

    // ------------------------------------------------------------------
    // Class-level setup: verify connectivity once before any test runs
    // ------------------------------------------------------------------

    [Before(Class)]
    public static async Task VerifyPgvectorReachable()
    {
        try
        {
            var builder = new NpgsqlDataSourceBuilder(ConnectionString);
            builder.UseVector();
            await using NpgsqlDataSource probe = builder.Build();
            await using NpgsqlConnection connection = await probe.OpenConnectionAsync();
            _pgvectorAvailable = true;

            // Build the shared data source used for the full test run
            var dsBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
            dsBuilder.UseVector();
            _dataSource = dsBuilder.Build();
        }
        catch
        {
            _pgvectorAvailable = false;
        }
    }

    [After(Class)]
    public static async Task DropTestTable()
    {
        if (_dataSource is null || !_pgvectorAvailable)
        {
            return;
        }

        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand($"DROP TABLE IF EXISTS {TableName}", connection);
        await cmd.ExecuteNonQueryAsync();
        await _dataSource.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // Per-test setup: build SUT and seed baseline data
    // ------------------------------------------------------------------

    [Before(Test)]
    public async Task SetUpAsync()
    {
        SkipIfNotAvailable();

        var options = Options.Create(new PgVectorOptions
        {
            ConnectionString = ConnectionString,
            TableName = TableName,
            Dimensions = Dimensions
        });

        _sut = new PgVectorStoreService(_dataSource!, options, new FakeLogger<PgVectorStoreService>());

        // Seed a known baseline so every test starts from the same state.
        // The schema is created lazily on first upsert.
        await SeedBaselineChunksAsync();
    }

    public void Dispose()
    {
        // NpgsqlDataSource is shared — disposed in [After(Class)].
    }

    // ------------------------------------------------------------------
    // Helper factories
    // ------------------------------------------------------------------

    private static readonly Guid DocumentA = Guid.NewGuid();
    private static readonly Guid DocumentB = Guid.NewGuid();

    /// <summary>
    /// Seeds a predictable set of chunks across two documents and all ChunkType values.
    /// Vectors are 3-dimensional unit vectors so cosine similarity is easy to reason about.
    ///   [1, 0, 0] — X-axis, used for Text chunks
    ///   [0, 1, 0] — Y-axis, used for Table chunks
    ///   [0, 0, 1] — Z-axis, used for FormField chunks
    /// </summary>
    private async Task SeedBaselineChunksAsync()
    {
        var chunks = new List<EmbeddedChunk>
        {
            // Document A — three chunks, one per ChunkType
            MakeChunk("doc-a-text-0",    DocumentA, 0, ChunkType.Text,      [1f, 0f, 0f], "Text chunk zero in doc A"),
            MakeChunk("doc-a-table-1",   DocumentA, 1, ChunkType.Table,     [0f, 1f, 0f], "Table chunk one in doc A"),
            MakeChunk("doc-a-form-2",    DocumentA, 2, ChunkType.FormField, [0f, 0f, 1f], "FormField chunk two in doc A"),

            // Document B — two Text chunks so we can verify cross-document isolation
            MakeChunk("doc-b-text-0",    DocumentB, 0, ChunkType.Text,      [1f, 0f, 0f], "Text chunk zero in doc B"),
            MakeChunk("doc-b-table-1",   DocumentB, 1, ChunkType.Table,     [0f, 1f, 0f], "Table chunk one in doc B"),
        };

        await _sut.UpsertChunksAsync(chunks);
    }

    private static EmbeddedChunk MakeChunk(
        string chunkId,
        Guid documentId,
        int index,
        ChunkType type,
        float[] embedding,
        string content = "content") =>
        new()
        {
            ChunkId = chunkId,
            DocumentId = documentId,
            ChunkIndex = index,
            ChunkType = type,
            Content = content,
            PageNumbers = [index + 1],
            TokenCount = 10,
            Embedding = embedding
        };

    private void SkipIfNotAvailable()
    {
        if (!_pgvectorAvailable)
        {
            Skip.Test(
                "pgvector container is not reachable on localhost:5433. " +
                "Start it with: docker compose up -d");
        }
    }

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    #region Ordering and scoring

    [Test]
    public async Task SearchAsync_WithMatchingEmbedding_ReturnsChunksOrderedByScore()
    {
        // Arrange — query close to Y-axis; Table chunk should rank higher than FormField chunk
        float[] query = [0.1f, 0.9f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 3);

        // Assert — scores must be non-increasing
        await Assert.That(results.Count).IsGreaterThan(1);

        for (int i = 0; i < results.Count - 1; i++)
        {
            await Assert.That(results[i].Score).IsGreaterThanOrEqualTo(results[i + 1].Score);
        }
    }

    [Test]
    public async Task SearchAsync_ReturnsScoresBetweenZeroAndOne()
    {
        // Arrange
        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 3);

        // Assert
        await Assert.That(results).IsNotEmpty();

        foreach (RetrievedChunk chunk in results)
        {
            await Assert.That(chunk.Score).IsGreaterThanOrEqualTo(0.0);
            await Assert.That(chunk.Score).IsLessThanOrEqualTo(1.0);
        }
    }

    [Test]
    public async Task SearchAsync_WithIdenticalEmbedding_ReturnsScoreNearOne()
    {
        // Arrange — exact match on the X-axis vector seeded as "doc-a-text-0"
        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 1);

        // Assert
        await Assert.That(results).IsNotEmpty();
        await Assert.That(results[0].Score).IsGreaterThan(0.99);
    }

    #endregion

    #region topK limiting

    [Test]
    public async Task SearchAsync_WithTopK_ReturnsNoMoreThanTopKResults()
    {
        // Arrange
        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 2);

        // Assert
        await Assert.That(results.Count).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task SearchAsync_WithTopKLargerThanAvailable_ReturnsAllAvailable()
    {
        // Arrange — Document A has 3 chunks; ask for 100
        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 100);

        // Assert — should get all 3, not error out
        await Assert.That(results.Count).IsEqualTo(3);
    }

    #endregion

    #region Document ID filtering

    [Test]
    public async Task SearchAsync_WithDocumentIdFilter_ReturnsOnlyChunksForThatDocument()
    {
        // Arrange
        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 10);

        // Assert — every returned chunk must belong to Document A
        await Assert.That(results).IsNotEmpty();

        foreach (RetrievedChunk chunk in results)
        {
            await Assert.That(chunk.DocumentId).IsEqualTo(DocumentA);
        }
    }

    [Test]
    public async Task SearchAsync_WithMultipleDocuments_DoesNotReturnCrossDocumentChunks()
    {
        // Arrange — both documents have identical X-axis vectors; querying DocumentB
        // should never return DocumentA chunks
        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> resultsForB =
            await _sut.SearchAsync(query, DocumentB, topK: 10);

        // Assert
        await Assert.That(resultsForB).IsNotEmpty();

        foreach (RetrievedChunk chunk in resultsForB)
        {
            await Assert.That(chunk.DocumentId).IsEqualTo(DocumentB);
        }
    }

    [Test]
    public async Task SearchAsync_WhenNoChunksMatchDocument_ReturnsEmptyList()
    {
        // Arrange — a document ID that was never seeded
        Guid unknownDocumentId = Guid.NewGuid();
        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, unknownDocumentId, topK: 10);

        // Assert
        await Assert.That(results).IsEmpty();
    }

    #endregion

    #region Empty table

    [Test]
    public async Task SearchAsync_WhenNoChunksExist_ReturnsEmptyList()
    {
        // Arrange — create a fresh service instance pointing at a brand-new empty table
        string emptyTable = $"test_empty_{Guid.NewGuid().ToString("N")[..8]}";
        var options = Options.Create(new PgVectorOptions
        {
            ConnectionString = ConnectionString,
            TableName = emptyTable,
            Dimensions = Dimensions
        });

        var freshSut = new PgVectorStoreService(
            _dataSource!,
            options,
            new FakeLogger<PgVectorStoreService>());

        // Trigger schema creation without seeding any rows
        await freshSut.UpsertChunksAsync([]);

        // Force schema init by upserting an empty list — schema is lazy,
        // so we need at least one round-trip. Use a dummy upsert of zero items
        // then drop and recreate to guarantee empty.
        // Simpler: just create the table via a direct DDL call.
        await using NpgsqlConnection connection = await _dataSource!.OpenConnectionAsync();
        string ddl = $"""
            CREATE EXTENSION IF NOT EXISTS vector;
            CREATE TABLE IF NOT EXISTS {emptyTable} (
                id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                document_id  UUID NOT NULL,
                chunk_id     TEXT NOT NULL UNIQUE,
                chunk_index  INTEGER NOT NULL,
                chunk_type   TEXT NOT NULL,
                page_numbers INTEGER[] NOT NULL,
                content      TEXT NOT NULL,
                token_count  INTEGER NOT NULL,
                embedding    vector({Dimensions}) NOT NULL
            );
            """;
        await using var ddlCmd = new NpgsqlCommand(ddl, connection);
        await ddlCmd.ExecuteNonQueryAsync();

        float[] query = [1f, 0f, 0f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await freshSut.SearchAsync(query, Guid.NewGuid(), topK: 10);

        // Assert
        await Assert.That(results).IsEmpty();

        // Cleanup
        await using var dropCmd = new NpgsqlCommand(
            $"DROP TABLE IF EXISTS {emptyTable}", connection);
        await dropCmd.ExecuteNonQueryAsync();
    }

    #endregion

    #region ChunkType filtering

    [Test]
    public async Task SearchAsync_WithChunkTypeFilter_ReturnsOnlyMatchingTypes()
    {
        // Arrange
        float[] query = [0.5f, 0.5f, 0f];
        IReadOnlyList<ChunkType> filter = [ChunkType.Text];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 10, chunkTypeFilter: filter);

        // Assert — all returned chunks must be Text type
        await Assert.That(results).IsNotEmpty();

        foreach (RetrievedChunk chunk in results)
        {
            await Assert.That(chunk.ChunkType).IsEqualTo(ChunkType.Text);
        }
    }

    [Test]
    public async Task SearchAsync_WithMultipleChunkTypeFilters_ReturnsAllMatchingTypes()
    {
        // Arrange
        float[] query = [0.5f, 0.5f, 0f];
        IReadOnlyList<ChunkType> filter = [ChunkType.Text, ChunkType.Table];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 10, chunkTypeFilter: filter);

        // Assert — should contain both Text and Table chunks, never FormField
        await Assert.That(results).IsNotEmpty();

        foreach (RetrievedChunk chunk in results)
        {
            await Assert.That(chunk.ChunkType).IsNotEqualTo(ChunkType.FormField);
        }

        bool hasText  = results.Any(c => c.ChunkType == ChunkType.Text);
        bool hasTable = results.Any(c => c.ChunkType == ChunkType.Table);
        await Assert.That(hasText).IsTrue();
        await Assert.That(hasTable).IsTrue();
    }

    [Test]
    public async Task SearchAsync_WithNullChunkTypeFilter_ReturnsAllTypes()
    {
        // Arrange
        float[] query = [0.5f, 0.5f, 0.5f];

        // Act
        IReadOnlyList<RetrievedChunk> results =
            await _sut.SearchAsync(query, DocumentA, topK: 10, chunkTypeFilter: null);

        // Assert — Document A has Text, Table, and FormField — all three should appear
        await Assert.That(results.Count).IsEqualTo(3);

        bool hasText      = results.Any(c => c.ChunkType == ChunkType.Text);
        bool hasTable     = results.Any(c => c.ChunkType == ChunkType.Table);
        bool hasFormField = results.Any(c => c.ChunkType == ChunkType.FormField);

        await Assert.That(hasText).IsTrue();
        await Assert.That(hasTable).IsTrue();
        await Assert.That(hasFormField).IsTrue();
    }

    #endregion
}
