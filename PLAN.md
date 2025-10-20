# Short plan — checklist

## Project Status Summary (Updated: 2025-10-20)

**Overall Progress:** ~45% Complete

### ✅ Completed (5/20)
- Project scaffold (solution, API, worker, Angular app)
- Storage & upload flow (blob storage, file validation)
- ProcessJob model & queueing (idempotency, Service Bus)
- Orchestration & worker plumbing (Durable Functions, all stage executors)
- Monitoring, telemetry & observability (Application Insights)
- Retries, idempotency & resiliency (retry policies, DLQ)

### 🟡 In Progress (9/20)
- OCR/layout extraction (executor exists, needs Azure Document Intelligence integration)
- Pre-processing & normalization (executor exists, needs core logic)
- Embeddings pipeline (executor exists, needs Azure OpenAI integration)
- Prompting & structured extraction (executor exists, needs LLM implementation)
- Validation & business rules (executor exists, needs validation logic)
- Persistence & outbox (database exists, needs extraction results schema & outbox)
- Human-in-the-loop UI (upload UI exists, needs review components)
- Security, PII & compliance (Managed Identity done, needs Key Vault & PII redaction)
- Testing & quality (test projects exist, needs expanded coverage)
- Runbook, docs & demo (CLAUDE.md exists, needs operational runbook)

### ❌ Not Started (6/20)
- Chunking strategy (semantic-aware chunker with metadata)
- RAG retrieval layer (vector search API)
- Cost controls & model-mix (cost tracking, model selection)
- ModelOps & dataset improvements (correction pipeline, A/B testing)

---

## ~~Project scaffold~~

- ~~Tasks: create repo, solution + projects (API, worker, shared DTOs, data migrations), Angular shell app.~~

- ~~Acceptance: dotnet build succeeds, Angular dev server runs.~~

- **Status: COMPLETED** - Solution structure with API (DocProcessing.Api), Worker Orchestrator (DocProcessing.Orchestrator), Domain/Application/Infrastructure layers, and Angular receiver-app all exist and build successfully.

## ~~Storage + upload flow~~

- ~~Tasks: API endpoint to request signed upload URL (Azure Blob SAS), client uploads PDF directly to blob; validate file type/size server-side, optional virus scan hook.~~

- ~~Acceptance: client can upload a PDF and server stores metadata (blob URL, size, mime).~~

- **Status: COMPLETED** - UploadFunctions.cs implements multipart/form-data upload, BlobStorageService handles Azure Blob Storage with Managed Identity, file validation exists.

## ~~ProcessJob model & queueing~~

- ~~Tasks: create ProcessJob DB entity (id, documentId, status, idempotencyToken, attempts, metadata); API creates record and sends message to Service Bus with id + token.~~

- ~~Acceptance: DB row created + message visible on queue; duplicate requests with same idempotency token are safely deduped.~~

- **Status: COMPLETED** - ProcessJob entity with JobId, DocumentId, IdempotencyKey, Status enum (Pending/Processing/Completed/Failed/ManualReview), Stage enum (Uploaded/OCR/Preprocess/Embed/Extract/Validate/Persist/Notify), EF migrations applied, Service Bus integration exists.

## ~~Orchestration & worker plumbing~~

- ~~Tasks: implement orchestrator (Durable Functions or worker service) that consumes queue messages, updates job status, and coordinates steps; implement retry & DLQ logic.~~

- ~~Acceptance: message processing changes ProcessJob status through states (Queued → Processing → Completed/ManualReview/Failed); failed jobs end up in DLQ after retries.~~

- **Status: COMPLETED** - DocumentProcessingOrchestrator (Durable Functions) with DocumentIngestionTrigger, all stage executors implemented (OCR, Preprocess, Embed, Extract, Validate, Persist, Notify), StartJob/CompleteJob/FailJob activities exist.

## OCR / layout extraction integration

- Tasks: integrate chosen OCR (e.g., Azure Form Recognizer) to extract text blocks, tables, coordinates, confidence; normalize output shape into internal DTOs.

- Acceptance: for sample PDFs you get structured blocks + tables with coordinates and confidence scores.

- **Status: IN PROGRESS** - OcrStageExecutor skeleton exists in orchestrator with TODO comments indicating Azure Document Intelligence integration planned. Implementation needed for actual OCR API calls, text/table extraction, and result storage.

## Pre-processing & normalization

- Tasks: text normalization (whitespace, ligatures, Unicode normalization), table → CSV/JSON for numeric fields, date/currency normalization rules, handle multi-column layouts via coordinates.

- Acceptance: normalized text + structured table exports that preserve numeric formats.

- **Status: IN PROGRESS** - PreprocessStageExecutor exists with activity infrastructure. Core normalization logic (whitespace, Unicode, date/currency parsing, table handling) needs implementation.

## Chunking strategy

- Tasks: design semantic-aware chunker (keep sentences/tables intact, attach coords & source IDs), compute chunk metadata (start/end offsets, sourceId).

- Acceptance: search chunks are sized to control token count and each chunk contains sourceId + offsets.

- **Status: NOT STARTED** - No chunking logic found. Should be implemented as part of preprocess or as separate stage before embedding.

## Embeddings pipeline

- Tasks: batch embedding calls, cache embeddings by document hash, store vectors in chosen vector store (Azure Cognitive Search vector index / Redis / Pinecone / pgvector).

- Acceptance: embeddings stored and retrievable by vector similarity; embedding cache avoids re-computation for same document hash.

- **Status: IN PROGRESS** - EmbedStageExecutor and EmbedStageActivity exist with comments indicating Azure OpenAI embedding generation and vector database storage planned. Need to implement actual embedding service, vector store integration, and caching logic.

## RAG retrieval layer

- Tasks: implement vector search API to fetch top-k chunks; incorporate exact table rows when applicable; expose retrieval metadata (score, offsets).

- Acceptance: retrieval returns useful contextual chunks with source citations for sample queries.

- **Status: NOT STARTED** - No retrieval service found. Need to implement vector search API, chunk retrieval with metadata, and source citation tracking.

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

- **Status: COMPLETED** - Application Insights integrated throughout API and orchestrator with structured logging via ILogger, correlation IDs tracked in ProcessJob, host.json configured for telemetry. Custom metrics for token usage/costs would enhance this further.

## ~~Retries, idempotency & resiliency~~

- ~~Tasks: implement retry policies (Polly) for external calls, idempotent job handling by token, DLQ handling, compensating actions for partial failure.~~

- ~~Acceptance: transient failures retried; repeated messages with same token do not create duplicate work.~~

- **Status: COMPLETED** - Idempotency via ProcessJob.IdempotencyKey with unique constraint, Attempts counter tracks retries, Service Bus DLQ configured, Durable Functions provides retry orchestration. RetryJob API endpoint exists for manual retries.

## Security, PII & compliance

- Tasks: Key Vault + managed identities, redact PII before sending to external LLMs when required, VNet/private endpoints or self-hosted models for sensitive data, retention policies.

- Acceptance: secrets not in source, PII redaction configurable, and retention rules enforced.

- **Status: IN PROGRESS** - Managed Identity configured for Azure services (Blob Storage, Service Bus, SQL). Need to integrate Key Vault for secrets, implement PII redaction service, configure VNet/private endpoints, and add retention policies.

## Cost controls & model-mix

- Tasks: implement model selection config (cheap models for embeddings/prelim, large for hard cases), batching, quotaing per customer, cost per document metrics.

- Acceptance: system logs model usage and cost metrics; admin can set quotas.

- **Status: NOT STARTED** - No cost tracking or model selection configuration found. Need to implement: model configuration per stage, token/cost tracking middleware, quota enforcement service, and cost metrics publishing to Application Insights.

## Testing & quality

- Tasks: unit tests for parsers/rule engine, integration tests mocking OCR/LLM SDKs, golden dataset regression tests, E2E test harness for sample PDFs.

- Acceptance: CI runs tests; golden regression flags prompt drift or accuracy regressions.

- **Status: IN PROGRESS** - Test projects exist (DocProcessing.Api.Tests, Infrastructure.Tests, ServiceBusQueueInspector.Tests). BlobStorageServiceTests found. Need to expand unit test coverage for all layers, add integration tests with mocked external services, create golden dataset tests, and implement E2E test harness.

## ModelOps & dataset improvements

- Tasks: collect reviewer corrections into training/validation sets, version prompts and models, A/B test prompt/model changes, track field precision/recall.

- Acceptance: ability to roll back to previous prompt/model; measurable improvement from retraining or prompt updates.

- **Status: NOT STARTED** - No ModelOps infrastructure found. Need to implement: correction data collection pipeline, prompt versioning system, A/B testing framework, precision/recall metrics tracking, and model rollback mechanism.

## Runbook, docs & demo

- Tasks: operational runbook (how to recover DLQ, restart workers, revoke keys), README for components, demo script and sample PDFs for interviews.

- Acceptance: team member can run demo and follow runbook to recover common failures.

- **Status: IN PROGRESS** - Comprehensive CLAUDE.md exists with architecture, commands, workflows, and development setup. README.md exists. Need to add: operational runbook for failure recovery, DLQ recovery procedures, demo script with sample PDFs, and troubleshooting guide.
