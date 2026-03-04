# Decision Log

Chronological record of non-obvious business and design decisions. Append-only — new entries go at the top.

---

## 2026-03-04 — ChunkType enum placed in Domain layer

**Context:** The `ChunkType` enum (Text, Table, FormField) identifies the origin content type of a document chunk. It could live in `Application.Models.Chunking` (co-located with the chunk models) or in `Domain.Entities` (alongside other domain enums).

**Decision:** Placed in `Domain.Entities` because ChunkType represents a domain concept that classifies document content, not an application-layer concern. This is consistent with `ProcessJobStatus` and `ProcessJobStage` which are also domain enums used across layers.

**Alternatives considered:** Keeping it in `Application.Models.Chunking` was simpler (fewer cross-layer references) but would have required the Domain layer to depend on Application for any future domain logic involving chunk types — violating Clean Architecture dependency direction.

**Affected areas:** [Document Chunking](document-chunking.md)

---

## 2026-03-04 — Single oversized items included as-is in chunks

**Context:** A single sentence or table can exceed the configured `MaxChunkSize` token limit. Options were: (a) include as-is, (b) force-split at character boundaries, (c) reject and fail the stage.

**Decision:** Include as-is (option a), accepting that some chunks may exceed `MaxChunkSize`. This prevents infinite loops when content cannot be split at sentence boundaries, avoids data loss, and preserves semantic coherence. Downstream embedding models can handle slightly oversized inputs with truncation if needed.

**Alternatives considered:** Force-splitting mid-sentence would break semantic meaning and produce poor embeddings. Rejecting would cause stage failures for legitimate documents with long paragraphs.

**Affected areas:** [Document Chunking](document-chunking.md)

---

## 2026-03-04 — Sentence-boundary chunking over fixed-size windowing

**Context:** The chunking stage needs to split preprocessed document text into pieces suitable for embedding models. Two main approaches: (a) fixed-size character/token windows with overlap, (b) sentence-boundary splitting with configurable overlap.

**Decision:** Sentence-boundary splitting (option b) with regex-based sentence detection (`(?<=[.!?])\s+(?=[A-Z\n])`), configurable max chunk size (default 512 tokens), and configurable overlap (default 50 tokens). This preserves semantic coherence — chunks align with natural language boundaries rather than cutting mid-thought.

**Alternatives considered:** Fixed-size windowing is simpler to implement and guarantees uniform chunk sizes, but produces chunks that split mid-sentence, degrading embedding quality and downstream extraction accuracy.

**Affected areas:** [Document Chunking](document-chunking.md)
