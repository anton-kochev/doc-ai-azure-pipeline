using DocProcessing.Application.Models.Embedding;

namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Stores embedded document chunks in a vector database for similarity search.
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Upserts a batch of embedded chunks into the vector store.
    /// </summary>
    /// <param name="chunks">The embedded chunks to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UpsertChunksAsync(IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken = default);
}
