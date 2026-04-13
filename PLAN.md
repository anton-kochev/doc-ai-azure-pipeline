# Short plan — checklist

## Project Status Summary (Updated: 2026-04-05)

**Overall Progress:** ~65% Complete (12/20)

### ✅ Completed (12/20)

- Project scaffold (solution, API, worker, Angular app)
- Storage & upload flow (blob storage, file validation)
- ProcessJob model & queueing (idempotency, Service Bus)
- Orchestration & worker plumbing (Durable Functions, all stage executors)
- **OCR/layout extraction** (Azure Document Intelligence integration complete)
- **Pre-processing & normalization** (TextNormalizer, TableConverter, FieldParser — 65+ tests)
- **Chunking strategy** (DocumentChunker with sentence-boundary splitting, 3 chunk types, overlap)
- **Embeddings pipeline** (OpenAI/Azure OpenAI, pgvector + Azure AI Search dual vector store — 20 embed tests)
- **RAG retrieval layer** (RetrievalService, dual vector store search, score normalization, 53 retrieval tests)
- Monitoring, telemetry & observability (Application Insights, correlation IDs)
- Retries, idempotency & resiliency (retry policies, DLQ)
- Testing & quality (437 tests — 400 succeeded, 37 skipped)

### 🔜 Next Up — Core Pipeline Stages (3/20)

1. Prompting & structured extraction (executor exists, needs LLM implementation)
2. Validation & business rules (executor exists, needs validation logic)
3. Persistence & outbox (database exists, needs extraction results schema & outbox)

### 🟡 Supporting Features (5/20)

- Human-in-the-loop UI (upload UI exists, needs review components)
- Security, PII & compliance (Managed Identity done, needs Key Vault & PII redaction)
- Cost controls & model-mix (cost tracking, model selection)
- ModelOps & dataset improvements (correction pipeline, A/B testing)
- Runbook, docs & demo (CLAUDE.md exists, needs operational runbook)

---

## ~~Project scaffold~~

- ~~Tasks: create repo, solution + projects (API, worker, shared DTOs, data migrations), Angular shell app.~~

- ~~Acceptance: dotnet build succeeds, Angular dev server runs.~~

- **Status: COMPLETED** - Solution structure with API (docproc/src/api/), Worker Orchestrator (docproc/src/worker/orchestrator/DocProcessing.Orchestrator/), Domain/Application/Infrastructure layers in docproc/src/common/, and Angular receiver-app all exist and build successfully. Comprehensive test projects with 97+ tests using FakeLogger and TimeProvider.

## ~~Storage + upload flow~~

- ~~Tasks: API endpoint to request signed upload URL (Azure Blob SAS), client uploads PDF directly to blob; validate file type/size server-side, optional virus scan hook.~~

- ~~Acceptance: client can upload a PDF and server stores metadata (blob URL, size, mime).~~

- **Status: COMPLETED** - UploadFunctions.cs implements multipart/form-data upload, BlobStorageService handles Azure Blob Storage with Managed Identity, file validation exists. Comprehensive BlobStorageServiceTests verify all functionality.

## ~~ProcessJob model & queueing~~

- ~~Tasks: create ProcessJob DB entity (id, documentId, status, idempotencyToken, attempts, metadata); API creates record and sends message to Service Bus with id + token.~~

- ~~Acceptance: DB row created + message visible on queue; duplicate requests with same idempotency token are safely deduped.~~

- **Status: COMPLETED** - ProcessJob entity with JobId, DocumentId, IdempotencyKey, CorrelationId, Status enum (Pending/Processing/Completed/Failed/ManualReview), Stage enum (Uploaded/OCR/Preprocess/Chunk/Embed/Extract/Validate/Persist/Notify), EF migrations applied, Service Bus abstraction with simplified message schema, correlation ID tracking throughout pipeline. ProcessJobService has 97+ tests with comprehensive coverage including idempotency, state transitions, and concurrency control.

## ~~Orchestration & worker plumbing~~

- ~~Tasks: implement orchestrator (Durable Functions or worker service) that consumes queue messages, updates job status, and coordinates steps; implement retry & DLQ logic.~~

- ~~Acceptance: message processing changes ProcessJob status through states (Queued → Processing → Completed/ManualReview/Failed); failed jobs end up in DLQ after retries.~~

- **Status: COMPLETED** - DocumentProcessingOrchestrator (Durable Functions) with DocumentIngestionTrigger, all stage executors implemented (OCR, Preprocess, Embed, Extract, Validate, Persist, Notify), StartJob/CompleteJob/FailJob activities exist. TimeProvider injected into all executors for testability. Service Bus abstraction simplifies messaging and enables better testing.

## ~~OCR / layout extraction integration~~ ✅

- ~~Tasks: integrate chosen OCR (e.g., Azure Form Recognizer) to extract text blocks, tables, coordinates, confidence; normalize output shape into internal DTOs.~~

- ~~Acceptance: for sample PDFs you get structured blocks + tables with coordinates and confidence scores.~~

- **Status: COMPLETED** (2025-12-28) - Full Azure Document Intelligence SDK integration implemented in `AzureDocumentIntelligenceOcrService` using Azure.AI.DocumentIntelligence v1.0.0. Features: Managed Identity authentication (DefaultAzureCredential), text extraction with coordinates and confidence scores, table detection with cell structures, form field (key-value) extraction, bounding box normalization (0.0-1.0 range), comprehensive error handling (OcrProcessingException), structured logging (EventIds 3001-3004), 32 unit tests (7 active validation tests passing), DI registration in Infrastructure layer, configuration in appsettings.json. Total test count: 149 tests (124 passing, 25 OCR implementation tests documented for integration testing).

## ~~Pre-processing & normalization~~ ✅

- ~~Tasks: text normalization (whitespace, ligatures, Unicode normalization), table → CSV/JSON for numeric fields, date/currency normalization rules, handle multi-column layouts via coordinates.~~

- ~~Acceptance: normalized text + structured table exports that preserve numeric formats.~~

- **Status: COMPLETED** (2026-03-04) - TextNormalizer (whitespace/NFC/ligatures), TableConverter (CSV/JSON with escaping), FieldParser (date/currency/number parsing) all implemented and tested. PreprocessStageExecutor bug fixed (was discarding activity result). 65+ characterization tests with documented BUG/QUIRK comments. Known issues: TableConverter column span bug, 4-symbol-only currency support, US-first date ambiguity.

## ~~Chunking strategy~~ ✅

- ~~Tasks: design semantic-aware chunker (keep sentences/tables intact, attach coords & source IDs), compute chunk metadata (start/end offsets, sourceId).~~

- ~~Acceptance: search chunks are sized to control token count and each chunk contains sourceId + offsets.~~

- **Status: COMPLETED** (2026-03-04) - DocumentChunker with sentence-boundary splitting (regex-based), 3 chunk types (Text, Table, FormField), configurable max chunk size (512 tokens default), overlap (50 tokens default), token estimation factor (1.3x). ChunkStageActivity, ChunkStageExecutor, StageMetadataKeys centralized constants. Full metadata tracking: page numbers, character offsets, source block lineage, token counts. Models: DocumentChunk (sealed record), ChunkMetadata, ChunkResult, ChunkType enum (Domain layer). Business logic documented in `docs/business-logic/document-chunking.md`.

## Embeddings pipeline

- Tasks: batch embedding calls, cache embeddings by document hash, store vectors in chosen vector store (Azure Cognitive Search vector index / Redis / Pinecone / pgvector).

- Acceptance: embeddings stored and retrievable by vector similarity; embedding cache avoids re-computation for same document hash.

- **Status: COMPLETED** (2026-04-05) - Full embedding pipeline: OpenAIEmbeddingService (supports both Azure OpenAI and plain OpenAI via config), dual vector store (PgVectorStoreService for local dev, AzureSearchVectorStoreService for production), EmbedStageActivity with batching, error handling (StageResult.Failure pattern), blob storage for EmbedResult. 20 TUnit tests covering guards, validation, batching, error handling, logging, cancellation. SQL injection protection, singleton NpgsqlDataSource, schema init safety. Business logic documented in `docs/business-logic/embeddings-pipeline.md`. Docker-compose local dev with pgvector on port 5433.

## RAG retrieval layer

- Tasks: implement vector search API to fetch top-k chunks; incorporate exact table rows when applicable; expose retrieval metadata (score, offsets).

- Acceptance: retrieval returns useful contextual chunks with source citations for sample queries.

- **Status: COMPLETED** (2026-04-13) - Full RAG retrieval layer: RetrievalService (Application layer) orchestrates query embedding + vector search + score threshold filtering. IVectorStoreService.SearchAsync implemented on both backends — PgVectorStoreService (cosine distance with IVFFlat index) and AzureSearchVectorStoreService (HNSW vector search with score normalization). AzureSearchVectorStoreService refactored to accept SearchClient/SearchIndexClient via DI for testability. Azure score normalization converts `1/(1+cosine_distance)` to cosine similarity. SearchAsync is read-only (no EnsureIndexExistsAsync). Models: RetrievalQuery, RetrievedChunk (with normalized score), RetrievalResult. RetrievalOptions (DefaultTopK=10, DefaultScoreThreshold=0.3, MaxTopK=50). RetrievalFailedException domain exception. 53 retrieval tests: 34 RetrievalService unit tests, 15 Azure Search tests (filter construction, score normalization, result mapping), 4 exception tests. 12 pgvector integration tests (skipped when Docker not running). Business logic documented in `docs/business-logic/retrieval-pipeline.md`.

## Prompting & structured extraction (LLM)

- Tasks: define strict system prompt, include JSON schema / function-call spec, add 1–3 few-shot examples, prepend retrieved chunks with source IDs, require citations + confidences.

- Acceptance: LLM returns strictly parseable JSON matching schema, with sourceId and confidence for each field.

- **Status: IN PROGRESS** - ExtractStageExecutor exists with TODO comments for profile-based extraction. Need to implement LLM prompting, JSON schema validation, few-shot examples, and citation tracking.

## Validation & business rules

- Tasks: implement field-level validators (type, ranges, cross-field checks), rule engine to detect contradictions/low confidence.

- Acceptance: validation outputs pass/fail per field and flags jobs that require human review.

- **Status: IN PROGRESS** - ValidateStageExecutor exists with infrastructure for business rules and confidence thresholds. Need to implement actual validation rules, field-level validators, and ManualReview flagging logic.

## Persistence & outbox/events

- Tasks: persist extraction results to MS SQL (documents, extractionItems, audit trail, reviewer flags); implement outbox pattern to publish domain events to Service Bus.

- Acceptance: database contains extraction records; events are published exactly once via outbox.

- **Status: IN PROGRESS** - PersistStageExecutor and database infrastructure exist with EF Core and migrations. Document and ProcessJob entities persist. Need to add extraction results schema, audit trail tables, and outbox pattern implementation.

## Human-in-the-loop UI

- Tasks: Angular review UI for manual corrections with context (original PDF viewer, highlighted source chunks, extracted JSON), API endpoints to submit corrections; corrections feed back into golden dataset.

- Acceptance: reviewer can edit fields, submit changes, and job status updates to Reviewed; corrections saved in audit trail.

- **Status: IN PROGRESS** - Angular receiver-app exists with upload functionality. Need to add review UI components (PDF viewer, extraction editor, submission workflow) and API endpoints for corrections.

## ~~Monitoring, telemetry & observability~~

- ~~Tasks: telemetry (App Insights / Prometheus), traces (OpenTelemetry), metrics for token usage, queue depth, latency, error rates; alerts for spike conditions.~~

- ~~Acceptance: dashboards show key metrics and alerts trigger on defined thresholds.~~

- **Status: COMPLETED** - Application Insights integrated throughout API and orchestrator with structured logging via ILogger and LoggerMessage source generators (EventId 1-15 in ProcessJobService), correlation IDs tracked in ProcessJob and propagated through all messages and logs (commit 3bcf59f), host.json configured for telemetry. FakeLogger infrastructure enables comprehensive logging verification in tests. Custom metrics for token usage/costs would enhance this further.

## ~~Retries, idempotency & resiliency~~

- ~~Tasks: implement retry policies (Polly) for external calls, idempotent job handling by token, DLQ handling, compensating actions for partial failure.~~

- ~~Acceptance: transient failures retried; repeated messages with same token do not create duplicate work.~~

- **Status: COMPLETED** - Idempotency via ProcessJob.IdempotencyKey with unique constraint and optimistic concurrency control using EF Core RowVersion, Attempts counter tracks retries with exponential backoff, Service Bus DLQ configured, Durable Functions provides retry orchestration. RetryJob API endpoint exists for manual retries. CancellationToken support throughout async operations. Comprehensive concurrency testing with 97+ tests.

## Security, PII & compliance

- Tasks: Key Vault + managed identities, redact PII before sending to external LLMs when required, VNet/private endpoints or self-hosted models for sensitive data, retention policies.

- Acceptance: secrets not in source, PII redaction configurable, and retention rules enforced.

- **Status: IN PROGRESS** - Managed Identity configured for Azure services (Blob Storage, Service Bus, SQL). Need to integrate Key Vault for secrets, implement PII redaction service, configure VNet/private endpoints, and add retention policies.

## Cost controls & model-mix

- Tasks: implement model selection config (cheap models for embeddings/prelim, large for hard cases), batching, quotaing per customer, cost per document metrics.

- Acceptance: system logs model usage and cost metrics; admin can set quotas.

- **Status: NOT STARTED** - No cost tracking or model selection configuration found. Need to implement: model configuration per stage, token/cost tracking middleware, quota enforcement service, and cost metrics publishing to Application Insights.

## ~~Testing & quality~~

- ~~Tasks: unit tests for parsers/rule engine, integration tests mocking OCR/LLM SDKs, golden dataset regression tests, E2E test harness for sample PDFs.~~

- ~~Acceptance: CI runs tests; golden regression flags prompt drift or accuracy regressions.~~

- **Status: COMPLETED** - 437 tests (400 succeeded, 37 skipped). 5 test projects: DocProcessing.Api.Tests, DocProcessing.Application.Tests, Infrastructure.Tests, DocProcessing.EndToEnd.Tests, DocProcessing.TestUtilities. Full coverage for ProcessJobService (idempotency, state transitions, concurrency), preprocessing (TextNormalizer, TableConverter, FieldParser), embedding pipeline (20 tests), retrieval layer (53 tests: RetrievalService unit tests, Azure Search search tests with score normalization, pgvector integration tests, exception tests), E2E pipeline flows with PipelineSimulator. FakeLogger, FakeTimeProvider, InMemoryDbContext, ControllableActivityFactory for test infrastructure. CI runs all tests on push.

## ModelOps & dataset improvements

- Tasks: collect reviewer corrections into training/validation sets, version prompts and models, A/B test prompt/model changes, track field precision/recall.

- Acceptance: ability to roll back to previous prompt/model; measurable improvement from retraining or prompt updates.

- **Status: NOT STARTED** - No ModelOps infrastructure found. Need to implement: correction data collection pipeline, prompt versioning system, A/B testing framework, precision/recall metrics tracking, and model rollback mechanism.

## Runbook, docs & demo

- Tasks: operational runbook (how to recover DLQ, restart workers, revoke keys), README for components, demo script and sample PDFs for interviews.

- Acceptance: team member can run demo and follow runbook to recover common failures.

- **Status: IN PROGRESS** - Comprehensive CLAUDE.md exists with architecture, commands, workflows, TDD requirements, and development setup. README.md standardized with docproc paths and clarified local development instructions (commit 6385568). Need to add: operational runbook for failure recovery, DLQ recovery procedures, demo script with sample PDFs, and troubleshooting guide.

---

## Recent Improvements (Oct 2025)

### Infrastructure & Architecture (Commits: 6385568, 3bcf59f, 6ce84a7, a214a75, 9ffea78, 69cecaf, 0a3ec4a)

1. **Correlation ID Tracking (3bcf59f)** - Added correlation IDs to ProcessJob entity and propagated through all logging and Service Bus messages for distributed tracing
2. **TimeProvider Injection (6ce84a7)** - Injected TimeProvider into orchestrator stage executors and updated tests for deterministic time-based testing
3. **Service Bus Abstraction (a214a75)** - Introduced Service Bus abstraction layer with simplified message schema, updated orchestrator and tests for better testability
4. **Project Reorganization (9ffea78)** - Moved API project to src/api and updated CI workflow, improved project structure
5. **Test Logging Standardization (69cecaf)** - Adopted FakeLogger/NullLogger across all test projects for consistent test logging
6. **OCR Pipeline Structure (0a3ec4a)** - Added OCR pipeline foundation, storage JSON helpers, and reorganized tests
7. **README Standardization (6385568)** - Updated README to use docproc paths, removed duplicates, clarified local development and run instructions

### Testing Infrastructure

- **97+ Unit Tests** covering:
  - Idempotency key computation (8 tests)
  - GetOrCreateJob scenarios (13 tests)
  - StartProcessing transitions (10 tests)
  - CompleteJob transitions (6 tests)
  - FailJob transitions (9 tests)
  - BlobStorageService functionality

- **Test Utilities** (DocProcessing.TestUtilities):
  - FakeLogger for verifying structured logging
  - FakeTimeProvider for deterministic time-based tests
  - Standardized test helpers across all projects

### Known Technical Debt

See `docproc/docs/TECH_DEBT_ProcessJob_State_Transitions.md` for detailed tracking:

**Resolved:**

- ✅ Concurrency race conditions with optimistic locking
- ✅ CancellationToken support throughout async operations
- ✅ Structured logging with LoggerMessage source generators
- ✅ ManualReview state machine — 3 service methods, 3 activity functions, external event handling, 46 tests
- ✅ Exception-based error handling — All 7 executors now propagate exceptions, boolean returns replaced by domain exceptions

**Pending:**

1. **Medium**: Repository pattern for separation of concerns

### Documentation & Tooling

- **CLAUDE.md**: Comprehensive project guide including:
  - Build and run commands for all components
  - TDD workflow requirements
  - Development workflows for common tasks
  - Local development setup
  - Architecture notes and configuration

### Next Priorities

1. **Prompting & structured extraction** — LLM prompting with JSON schema, few-shot examples
2. **Validation & business rules** — Field-level validators, confidence scoring, ManualReview flagging
3. **Persistence & outbox** — Extraction results schema, audit trail, outbox pattern
