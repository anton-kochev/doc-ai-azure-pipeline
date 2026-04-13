using DocProcessing.Application.Models.Embedding;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Stores and searches embedded document chunks in a vector database.
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Upserts a batch of embedded chunks into the vector store.
    /// </summary>
    /// <param name="chunks">The embedded chunks to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UpsertChunksAsync(IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for chunks similar to the provided embedding vector.
    /// </summary>
    /// <param name="queryEmbedding">The embedding vector of the search query.</param>
    /// <param name="documentId">Filter to chunks belonging to this document.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <param name="chunkTypeFilter">Optional filter for chunk types. Null = all types.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Chunks with similarity scores, ordered by descending relevance.</returns>
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        Guid documentId,
        int topK,
        IReadOnlyList<ChunkType>? chunkTypeFilter = null,
        CancellationToken cancellationToken = default);
}
