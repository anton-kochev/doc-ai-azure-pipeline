namespace DocProcessing.Infrastructure.Options;

/// <summary>
/// Configuration options for the pgvector (PostgreSQL) vector store.
/// </summary>
public sealed class PgVectorOptions
{
    /// <summary>
    /// Gets or sets the PostgreSQL connection string for the vector database.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the table name for storing document chunk embeddings.
    /// </summary>
    public string TableName { get; set; } = "document_chunks";

    /// <summary>
    /// Gets or sets the dimensionality of the embedding vectors.
    /// </summary>
    public int Dimensions { get; set; } = 1536;
}
