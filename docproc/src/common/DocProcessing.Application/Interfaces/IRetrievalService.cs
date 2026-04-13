using DocProcessing.Application.Models.Retrieval;

namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Orchestrates RAG retrieval: embeds the query, searches the vector store,
/// and applies score thresholds. Used by pipeline stages (Extract) to
/// fetch relevant context chunks.
/// </summary>
public interface IRetrievalService
{
    /// <summary>
    /// Retrieves the most relevant document chunks for a given query.
    /// </summary>
    /// <param name="query">The retrieval query with filters and parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Ranked chunks with relevance scores and citation metadata.</returns>
    Task<RetrievalResult> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default);
}
