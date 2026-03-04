using DocProcessing.Application.Configuration;
using DocProcessing.Application.Models.Chunking;
using DocProcessing.Application.Models.Preprocessing;

namespace DocProcessing.Application.Services.Chunking;

/// <summary>
/// Splits a preprocessed document into chunks ready for downstream embedding and extraction.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Chunks the supplied <paramref name="input"/> document according to <paramref name="options"/>.
    /// </summary>
    /// <param name="input">The preprocessed document to chunk.</param>
    /// <param name="options">Chunking configuration (size, overlap, token estimation factor).</param>
    /// <returns>
    /// An ordered list of <see cref="DocumentChunk"/> instances and a <see cref="ChunkMetadata"/>
    /// summary of the chunking operation.
    /// </returns>
    (IReadOnlyList<DocumentChunk> Chunks, ChunkMetadata Metadata) ChunkDocument(
        PreprocessResult input, ChunkingOptions options);
}
