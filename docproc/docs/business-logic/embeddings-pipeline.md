# Embeddings Pipeline

## Business Context

The embedding stage sits between document chunking and structured extraction in the pipeline:

```
PDF → OCR → Preprocessing → Chunking → [Embedding] → Extraction → Validation → Persist → Notify
```

Previous stages prepare the document: OCR extracts text from PDF, preprocessing normalizes it, and chunking splits it into small logical fragments (paragraphs, tables, form fields). The embedding stage converts those text fragments into numerical vectors — arrays of ~1,500 floating-point numbers — so the system can quickly find relevant fragments later.

The key property of embeddings: **texts with similar meaning produce similar vectors.** For example, "total contract value: $50,000" and "amount payable: fifty thousand dollars" will have vectors that are mathematically close. This is the foundation of RAG (Retrieval-Augmented Generation) — first find relevant chunks via vector similarity, then pass them to the LLM for structured extraction.

## What This Stage Does

1. **Reads chunk results** from the previous stage (stored in Blob Storage as JSON)
2. **Batches the chunks** — the embedding API has per-request limits, so chunks are sent in configurable batches (default: 100)
3. **Generates embeddings** — sends each chunk's text content to Azure OpenAI, receives back a numerical vector per chunk
4. **Stores vectors in a vector store** — a specialized database optimized for similarity search
5. **Saves the full result to Blob Storage** — as JSON for audit trail and reprocessing (avoids re-calling the paid API)

## Vector Store Architecture

Two vector store providers share a single interface, switched by configuration:

- **Local development:** pgvector (PostgreSQL extension) running in Docker. Free, easy to test locally.
- **Production:** Azure AI Search — managed Azure service with vector indexing, hybrid search (keyword + vector), and Managed Identity authentication.

The pipeline code has no knowledge of which provider is active. This is a deployment-time decision via a config value (`VectorStore:Provider`), not a runtime switch.

## Error Handling

If the embedding API is unavailable or the vector store fails, the stage returns a structured failure with an error code. The orchestrator (Durable Functions) automatically retries the entire stage. After all retry attempts are exhausted, the job transitions to Failed status. All failures are logged with correlation IDs for distributed tracing.

## Cost Considerations

Embedding API calls cost money (though `text-embedding-3-small` is one of the cheapest models). Results are persisted to blob storage so that if a job needs to be restarted from a later stage, embeddings are not regenerated.

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Embedding:DeploymentName` | `text-embedding-3-small` | Azure OpenAI deployment name |
| `Embedding:Dimensions` | `1536` | Vector dimensions |
| `Embedding:BatchSize` | `100` | Chunks per embedding API call |
| `Embedding:OutputBlobContainer` | `embed-results` | Blob container for results |
| `VectorStore:Provider` | `pgvector` | Active vector store (`pgvector` or `AzureSearch`) |

## Metadata Handoff

**Receives from Chunk stage:**
- `chunkBlobPath` — location of chunk results in blob storage

**Passes to Extract stage:**
- `embedBlobPath` — location of embedding results in blob storage
- `embeddedChunks` — total number of embedded chunks
- `embeddingModel` — model used for embedding generation
- `embeddingDimensions` — vector dimensions
