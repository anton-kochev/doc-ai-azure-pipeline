namespace DocProcessing.Domain.Entities;

/// <summary>
/// Identifies the origin content type of a document chunk.
/// </summary>
public enum ChunkType
{
    /// <summary>Plain prose or narrative text extracted from a page.</summary>
    Text,

    /// <summary>Serialised table content (rows/columns) extracted from a structured table.</summary>
    Table,

    /// <summary>Key-value content derived from a parsed form field.</summary>
    FormField
}
