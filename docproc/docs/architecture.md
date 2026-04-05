# Architecture & Pipeline Flow

## 1. System Architecture

How the major components connect. The client uploads documents to the API, which queues processing jobs. The Worker picks them up and drives them through the pipeline.

```mermaid
%%{init: {'theme': 'base', 'themeVariables': {
  'primaryColor': '#4A90D9',
  'primaryTextColor': '#fff',
  'primaryBorderColor': '#2E6BA6',
  'lineColor': '#5C6BC0',
  'secondaryColor': '#81C784',
  'tertiaryColor': '#FFB74D'
}}}%%
graph LR
    subgraph Client["🖥️ Client Layer"]
        UI["Angular App<br/><small>Upload & Review UI</small>"]
    end

    subgraph API["⚡ API Layer — Azure Functions"]
        Upload["POST /api/upload"]
        Retry["POST /api/jobs/.../retry"]
    end

    subgraph Messaging["📨 Messaging"]
        SB["Azure Service Bus<br/><small>documents.process queue</small>"]
    end

    subgraph Worker["⚙️ Worker — Durable Functions"]
        Orch["Orchestrator"]
        subgraph Stages["Pipeline Stages"]
            direction TB
            S1["OCR"] --> S2["Preprocess"]
            S2 --> S3["Chunk"]
            S3 --> S4["Embed"]
            S4 --> S5["Extract"]
            S5 --> S6["Validate"]
            S6 --> S7["Persist"]
            S7 --> S8["Notify"]
        end
        Orch --> Stages
    end

    subgraph Storage["☁️ Azure Services"]
        Blob["Blob Storage<br/><small>Documents & stage results</small>"]
        SQL["SQL Database<br/><small>Jobs, Documents, Profiles</small>"]
        PG["pgvector / AI Search<br/><small>Vector embeddings</small>"]
        OCR_SVC["Document Intelligence<br/><small>OCR engine</small>"]
        OAI["OpenAI<br/><small>Embeddings API</small>"]
    end

    UI -->|"HTTP multipart"| Upload
    UI -->|"HTTP POST"| Retry
    Upload -->|"1. Save file"| Blob
    Upload -->|"2. Create job"| SQL
    Upload -->|"3. Queue message"| SB
    SB -->|"Trigger"| Orch
    Stages -->|"Read/Write results"| Blob
    Stages -->|"Update job status"| SQL
    S1 -->|"Analyze document"| OCR_SVC
    S4 -->|"Generate vectors"| OAI
    S4 -->|"Store vectors"| PG

    style Client fill:#E3F2FD,stroke:#1565C0,color:#000
    style API fill:#E8F5E9,stroke:#2E7D32,color:#000
    style Messaging fill:#FFF3E0,stroke:#E65100,color:#000
    style Worker fill:#F3E5F5,stroke:#6A1B9A,color:#000
    style Storage fill:#ECEFF1,stroke:#546E7A,color:#000
```

## 2. Document Pipeline Flow

The journey of a single document from upload to completion. Green stages are fully implemented; dashed stages are scaffolded but not yet wired to real services.

```mermaid
%%{init: {'theme': 'base', 'themeVariables': {
  'primaryColor': '#4A90D9',
  'primaryTextColor': '#fff'
}}}%%
flowchart TD
    START(["📄 Document Uploaded"])

    START --> OCR

    subgraph DONE["✅ Implemented"]
        OCR["🔍 <b>OCR</b><br/><small>Azure Document Intelligence</small><br/><small>→ text blocks, tables, fields</small>"]
        PRE["🧹 <b>Preprocess</b><br/><small>Normalize Unicode, whitespace</small><br/><small>→ clean text, structured tables</small>"]
        CHK["✂️ <b>Chunk</b><br/><small>Sentence-boundary splitting</small><br/><small>→ Text / Table / FormField chunks</small>"]
        EMB["🧠 <b>Embed</b><br/><small>OpenAI text-embedding-3-small</small><br/><small>→ 1536-dim vectors in pgvector</small>"]

        OCR -->|"ocrBlobPath"| PRE
        PRE -->|"preprocessBlobPath"| CHK
        CHK -->|"chunkBlobPath"| EMB
    end

    subgraph TODO["🔜 Planned"]
        EXT["📋 <b>Extract</b><br/><small>LLM structured extraction</small><br/><small>→ JSON fields with citations</small>"]
        VAL["✓ <b>Validate</b><br/><small>Business rules, confidence</small><br/><small>→ pass / fail / manual review</small>"]
        PER["💾 <b>Persist</b><br/><small>Save results to SQL</small><br/><small>→ extraction records, audit trail</small>"]
        NOT["📬 <b>Notify</b><br/><small>Send completion events</small><br/><small>→ Service Bus / webhook</small>"]

        EXT -->|"extractBlobPath"| VAL
        VAL --> PER
        PER --> NOT
    end

    EMB -->|"embedBlobPath"| EXT
    NOT --> END(["✅ Job Completed"])

    VAL -->|"Low confidence"| MR(["👁️ Manual Review"])
    MR -->|"Resume / Reject"| PER

    style DONE fill:#E8F5E9,stroke:#2E7D32,color:#000
    style TODO fill:#FFF8E1,stroke:#F9A825,color:#000
    style START fill:#E3F2FD,stroke:#1565C0,color:#000
    style END fill:#E8F5E9,stroke:#2E7D32,color:#000
    style MR fill:#FCE4EC,stroke:#C62828,color:#000
```

## 3. Data & Metadata Flow

Each stage reads input from Blob Storage, processes it, writes output back, and passes metadata keys to the next stage via the orchestrator.

```mermaid
%%{init: {'theme': 'base'}}%%
flowchart LR
    subgraph BlobStorage["📦 Blob Storage"]
        direction TB
        B1["uploads/<br/><small>raw PDF</small>"]
        B2["ocr-results/<br/><small>OCR JSON</small>"]
        B3["preprocess-results/<br/><small>normalized text</small>"]
        B4["chunk-results/<br/><small>document chunks</small>"]
        B5["embed-results/<br/><small>chunks + vectors</small>"]
    end

    subgraph Pipeline["⚙️ Pipeline Stages"]
        direction TB
        OCR["OCR"]
        PRE["Preprocess"]
        CHK["Chunk"]
        EMB["Embed"]
    end

    subgraph MetadataKeys["🏷️ Metadata (accumulated)"]
        direction TB
        M1["blobPath<br/>blobContainer"]
        M2["+ocrBlobPath"]
        M3["+preprocessBlobPath"]
        M4["+chunkBlobPath<br/>+totalChunks<br/>+totalTokens"]
        M5["+embedBlobPath<br/>+embeddingModel<br/>+embeddingDimensions"]
    end

    B1 -->|"read"| OCR
    OCR -->|"write"| B2
    OCR -.->|"set"| M2

    B2 -->|"read"| PRE
    PRE -->|"write"| B3
    PRE -.->|"set"| M3

    B3 -->|"read"| CHK
    CHK -->|"write"| B4
    CHK -.->|"set"| M4

    B4 -->|"read"| EMB
    EMB -->|"write"| B5
    EMB -.->|"set"| M5

    M1 -.->|"input"| OCR
    M2 -.->|"input"| PRE
    M3 -.->|"input"| CHK
    M4 -.->|"input"| EMB

    style BlobStorage fill:#E3F2FD,stroke:#1565C0,color:#000
    style Pipeline fill:#E8F5E9,stroke:#2E7D32,color:#000
    style MetadataKeys fill:#FFF3E0,stroke:#E65100,color:#000
```

## 4. Local Development Stack

Everything runs with a single `docker compose up -d`:

```mermaid
%%{init: {'theme': 'base'}}%%
graph TB
    subgraph Docker["🐳 Docker Compose"]
        SQL["SQL Server<br/><small>:1433</small>"]
        PG["pgvector<br/><small>:5433</small>"]
        AZ["Azurite<br/><small>:10000-10002</small>"]
        SBE["Service Bus Emulator<br/><small>:5672</small>"]
    end

    subgraph Local["💻 Local Processes"]
        API["API Functions<br/><small>func start :7071</small>"]
        WRK["Worker Orchestrator<br/><small>func start :7072</small>"]
        NG["Angular Client<br/><small>npm start :4200</small>"]
    end

    subgraph Cloud["☁️ Cloud APIs"]
        OAI["OpenAI API<br/><small>embeddings</small>"]
    end

    NG -->|"HTTP"| API
    API --> SQL
    API --> AZ
    API --> SBE
    SBE --> WRK
    WRK --> SQL
    WRK --> AZ
    WRK --> PG
    WRK --> OAI

    style Docker fill:#E3F2FD,stroke:#1565C0,color:#000
    style Local fill:#E8F5E9,stroke:#2E7D32,color:#000
    style Cloud fill:#FFF3E0,stroke:#E65100,color:#000
```
