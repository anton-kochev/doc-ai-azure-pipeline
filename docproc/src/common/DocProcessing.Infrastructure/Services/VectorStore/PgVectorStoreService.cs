using System.Text.RegularExpressions;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Embedding;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Domain.Entities;
using DocProcessing.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace DocProcessing.Infrastructure.Services.VectorStore;

/// <summary>
/// Stores embedded document chunks in a PostgreSQL database with pgvector extension.
/// </summary>
public sealed partial class PgVectorStoreService : IVectorStoreService
{
    private static readonly Regex SafeIdentifier = new(@"^[a-z_][a-z0-9_]{0,62}$", RegexOptions.Compiled);

    private readonly PgVectorOptions _options;
    private readonly ILogger<PgVectorStoreService> _logger;
    private readonly NpgsqlDataSource _dataSource;

    private int _schemaInitialized;

    public PgVectorStoreService(
        NpgsqlDataSource dataSource,
        IOptions<PgVectorOptions> options,
        ILogger<PgVectorStoreService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _dataSource = dataSource;

        if (!SafeIdentifier.IsMatch(_options.TableName))
        {
            throw new ArgumentException(
                $"Invalid table name: '{_options.TableName}'. Must match ^[a-z_][a-z0-9_]{{0,62}}$",
                nameof(options));
        }

        if (_options.Dimensions is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Dimensions must be between 1 and 4096");
        }
    }

    /// <inheritdoc />
    public async Task UpsertChunksAsync(IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);

        LogUpsertingChunks(chunks.Count, chunks[0].DocumentId);

        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            string table = _options.TableName;
            string sql = $"""
                INSERT INTO {table}
                    (document_id, chunk_id, chunk_index, chunk_type, page_numbers, content, token_count, embedding)
                VALUES
                    ($1, $2, $3, $4, $5, $6, $7, $8)
                ON CONFLICT (chunk_id) DO UPDATE SET
                    document_id  = EXCLUDED.document_id,
                    chunk_index  = EXCLUDED.chunk_index,
                    chunk_type   = EXCLUDED.chunk_type,
                    page_numbers = EXCLUDED.page_numbers,
                    content      = EXCLUDED.content,
                    token_count  = EXCLUDED.token_count,
                    embedding    = EXCLUDED.embedding
                """;

            foreach (EmbeddedChunk chunk in chunks)
            {
                await using var cmd = new NpgsqlCommand(sql, connection, transaction);

                cmd.Parameters.Add(new NpgsqlParameter { Value = chunk.DocumentId, NpgsqlDbType = NpgsqlDbType.Uuid });
                cmd.Parameters.Add(new NpgsqlParameter { Value = chunk.ChunkId, NpgsqlDbType = NpgsqlDbType.Text });
                cmd.Parameters.Add(new NpgsqlParameter { Value = chunk.ChunkIndex, NpgsqlDbType = NpgsqlDbType.Integer });
                cmd.Parameters.Add(new NpgsqlParameter { Value = chunk.ChunkType.ToString(), NpgsqlDbType = NpgsqlDbType.Text });
                cmd.Parameters.Add(new NpgsqlParameter { Value = chunk.PageNumbers.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
                cmd.Parameters.Add(new NpgsqlParameter { Value = chunk.Content, NpgsqlDbType = NpgsqlDbType.Text });
                cmd.Parameters.Add(new NpgsqlParameter { Value = chunk.TokenCount, NpgsqlDbType = NpgsqlDbType.Integer });
                cmd.Parameters.Add(new NpgsqlParameter { Value = new Vector(chunk.Embedding) });

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            LogChunksUpserted(chunks.Count, chunks[0].DocumentId);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
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
        await EnsureSchemaAsync(cancellationToken);

        string table = _options.TableName;

        string chunkTypeClause = chunkTypeFilter is { Count: > 0 }
            ? "AND chunk_type = ANY($4)"
            : "";

        string sql = $"""
            SELECT chunk_id, document_id, chunk_index, content, chunk_type,
                   page_numbers, token_count,
                   1 - (embedding <=> $1) AS score
            FROM {table}
            WHERE document_id = $2
            {chunkTypeClause}
            ORDER BY embedding <=> $1
            LIMIT $3
            """;

        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);

        cmd.Parameters.Add(new NpgsqlParameter { Value = new Vector(queryEmbedding) });
        cmd.Parameters.Add(new NpgsqlParameter { Value = documentId, NpgsqlDbType = NpgsqlDbType.Uuid });
        cmd.Parameters.Add(new NpgsqlParameter { Value = topK, NpgsqlDbType = NpgsqlDbType.Integer });

        if (chunkTypeFilter is { Count: > 0 })
        {
            cmd.Parameters.Add(new NpgsqlParameter
            {
                Value = chunkTypeFilter.Select(t => t.ToString()).ToArray(),
                NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
            });
        }

        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<RetrievedChunk>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RetrievedChunk
            {
                ChunkId = reader.GetString(0),
                DocumentId = reader.GetGuid(1),
                ChunkIndex = reader.GetInt32(2),
                Content = reader.GetString(3),
                ChunkType = Enum.Parse<ChunkType>(reader.GetString(4)),
                PageNumbers = reader.GetFieldValue<int[]>(5),
                TokenCount = reader.GetInt32(6),
                Score = reader.GetDouble(7)
            });
        }

        LogSearchCompleted(results.Count, documentId);

        return results;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _schemaInitialized) == 1)
        {
            return;
        }

        string table = _options.TableName;
        int dimensions = _options.Dimensions;

        string ddl = $"""
            CREATE EXTENSION IF NOT EXISTS vector;

            CREATE TABLE IF NOT EXISTS {table} (
                id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                document_id  UUID NOT NULL,
                chunk_id     TEXT NOT NULL UNIQUE,
                chunk_index  INTEGER NOT NULL,
                chunk_type   TEXT NOT NULL,
                page_numbers INTEGER[] NOT NULL,
                content      TEXT NOT NULL,
                token_count  INTEGER NOT NULL,
                embedding    vector({dimensions}) NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_{table}_document_id ON {table}(document_id);

            CREATE INDEX IF NOT EXISTS idx_{table}_embedding
                ON {table} USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);
            """;

        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(ddl, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        Volatile.Write(ref _schemaInitialized, 1);
        LogSchemaInitialized(table, dimensions);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Upserting {Count} chunks for DocumentId={DocumentId}")]
    private partial void LogUpsertingChunks(int count, Guid documentId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Successfully upserted {Count} chunks for DocumentId={DocumentId}")]
    private partial void LogChunksUpserted(int count, Guid documentId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Vector store schema initialized: table={Table}, dimensions={Dimensions}")]
    private partial void LogSchemaInitialized(string table, int dimensions);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Vector search completed. ResultCount={ResultCount}, DocumentId={DocumentId}")]
    private partial void LogSearchCompleted(int resultCount, Guid documentId);
}
