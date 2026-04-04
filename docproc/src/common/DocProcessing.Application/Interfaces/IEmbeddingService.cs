namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Generates embedding vectors from text content using an external embedding model.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates embedding vectors for a batch of text inputs.
    /// </summary>
    /// <param name="texts">The text inputs to embed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An ordered list of embedding vectors, one per input text.</returns>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
