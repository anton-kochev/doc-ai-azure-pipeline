# Retrieval Pipeline (RAG)

## Business Context

The retrieval layer sits between the embedding stage and the extraction stage in the pipeline:

```
PDF → OCR → Preprocessing → Chunking → Embedding → [Retrieval] → Extraction → Validation → Persist → Notify
```

After the embedding stage stores document chunks as vectors, the retrieval layer finds the most relevant chunks for a given query using vector similarity search. This is the "R" in RAG (Retrieval-Augmented Generation) — the system retrieves relevant context before sending it to an LLM for structured extraction.

**Why retrieval matters:** A document may contain hundreds of chunks, but an LLM prompt has a limited context window and works best with focused, relevant context. Retrieval ensures only the most semantically relevant chunks are included in the extraction prompt, improving accuracy and reducing cost.

## What This Layer Does

1. **Embeds the query** — converts a natural-language question (e.g., "What is the invoice total?") into a vector using the same embedding model that encoded the document chunks
2. **Searches the vector store** — finds the top-k chunks most similar to the query vector, filtered to a specific document
3. **Applies score threshold** — discards chunks below a configurable relevance score to maintain quality
4. **Returns ranked results** — chunks ordered by relevance, with citation metadata (page numbers, chunk type, similarity score)

## Architecture

`RetrievalService` lives in the Application layer and orchestrates two interfaces:

- **`IEmbeddingService`** — embeds the query text (same service used by the embed stage)
- **`IVectorStoreService.SearchAsync`** — searches the vector store for similar chunks

Both vector store backends (pgvector and Azure AI Search) implement the same `SearchAsync` method. The retrieval service applies score threshold filtering at the Application layer for consistent behavior across providers.

## Key Design Decisions

### Score normalization at the provider boundary
Each vector store backend scores differently. pgvector computes `1 - cosine_distance` (true cosine similarity, range 0–1). Azure AI Search returns `1 / (1 + cosine_distance)`, a transformed ranking score that is NOT cosine similarity. Both providers normalize their scores to cosine similarity before returning results — pgvector does this in SQL, Azure Search does it via `NormalizeScore()` in `AzureSearchVectorStoreService`. This ensures threshold semantics are identical across providers.

### Score threshold filtering in the Application layer
With scores normalized to cosine similarity by both providers, the `RetrievalService` applies a single threshold filter at the Application layer. This is clean and consistent.

### Score boundary is inclusive (>=)
A chunk scoring exactly at the threshold is included. This was a deliberate choice — at the boundary, it's better to include slightly more context than to miss something relevant.

### No baked-in table boosting
Instead of hardcoding special handling for table chunks, `RetrievalQuery.ChunkTypeFilter` lets the caller (Extract stage) make targeted retrieval calls — e.g., one call for text chunks, another specifically for tables. This is more flexible and keeps the retrieval layer generic.

### Result count may be less than topK
After threshold filtering, the actual number of returned chunks may be fewer than the requested `topK`. This is intentional — quality over quantity. The `TotalCandidates` field reports how many chunks the vector store returned before filtering.

## Error Handling

Infrastructure failures (embedding API down, vector store connection error) are wrapped in `RetrievalFailedException` with the document ID and query text for context. `OperationCanceledException` propagates unwrapped. All failures are logged with structured logging and correlation IDs.

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Retrieval:DefaultTopK` | `10` | Default number of chunks to retrieve |
| `Retrieval:DefaultScoreThreshold` | `0.3` | Minimum similarity score (0.0–1.0) |
| `Retrieval:MaxTopK` | `50` | Maximum allowed topK to prevent excessive results |

These defaults can be overridden per-query via `RetrievalQuery.TopK` and `RetrievalQuery.ScoreThreshold`.

## Metadata Handoff

The retrieval layer produces metadata for downstream consumption by the Extract stage:

| Key | Type | Description |
|-----|------|-------------|
| `retrievedChunks` | int | Number of chunks returned after filtering |
| `retrievalTopK` | int | Effective topK used (after defaults/clamping) |
| `retrievalScoreThreshold` | double | Effective score threshold used |
| `retrievalTotalCandidates` | int | Raw count from vector store before filtering |
| `retrievalSearchDurationMs` | double | Vector search time in milliseconds |

## Vector Store Search Details

### pgvector (local development)
- Uses the cosine distance operator `<=>` for similarity search
- Score = `1 - cosine_distance` (higher = more similar)
- IVFFlat index on the embedding column (lists = 100)
- Filters by `document_id` and optionally by `chunk_type`

### Azure AI Search (production)
- Uses HNSW (Hierarchical Navigable Small World) algorithm for vector search
- `VectorizedQuery` with configurable `KNearestNeighborsCount`
- OData filter for `documentId` and `chunkType`
- Uses `SearchClient` injected via DI (testable with mocks)
- **Score normalization**: Azure returns `1 / (1 + cosine_distance)`, which is NOT cosine similarity. The service normalizes to cosine similarity via `2 - (1/score)`, clamped to [0, 1]. This ensures threshold semantics are consistent with pgvector.
- **Read-only search path**: `SearchAsync` does NOT call `EnsureIndexExistsAsync` — index creation happens only on the write path (`UpsertChunksAsync`). This keeps search permissions minimal.

## Models

### RetrievalQuery (input)
- `QueryText` — the natural-language search query
- `DocumentId` — restrict search to this document (required)
- `TopK` — max results (optional, defaults to config)
- `ScoreThreshold` — min score (optional, defaults to config)
- `ChunkTypeFilter` — restrict to specific chunk types (optional)

### RetrievedChunk (output)
- `ChunkId`, `DocumentId`, `ChunkIndex` — identity and position
- `Content` — the text content
- `ChunkType` — Text, Table, or FormField
- `PageNumbers` — source pages for citation
- `TokenCount` — token estimate
- `Score` — similarity score (0.0–1.0)

### RetrievalResult (aggregated output)
- `Chunks` — ranked list of `RetrievedChunk`
- `TotalCandidates` — count before threshold filtering
- `TotalTokens` — sum of token counts (computed)
- `SearchDuration`, `EmbeddingDuration` — timing metadata
