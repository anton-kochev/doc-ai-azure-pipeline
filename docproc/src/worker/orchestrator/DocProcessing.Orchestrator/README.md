# DocProcessing.Orchestrator - Durable Functions App

Azure Durable Functions app for orchestrating document processing workflows, built with .NET 8 isolated worker.

## Features

- **Service Bus Trigger**: Listens to `documents.process` queue for document processing requests
- **Durable Orchestrations**: Manages long-running document processing workflows
- **Message Validation**: Validates incoming messages for required fields and version compatibility
- **Application Insights**: Full telemetry and distributed tracing support
- **Idempotency**: Prevents duplicate processing using idempotency keys

## Configuration

### Local Development

Update `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnectionString": "Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<key-name>;SharedAccessKey=<key>",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=<key>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/"
  }
}
```

### Azure Deployment

Configure the following application settings:

| Setting | Description |
|---------|-------------|
| `AzureWebJobsStorage` | Storage account connection string for Durable Functions state |
| `ServiceBusConnectionString` | Service Bus namespace connection string (supports Managed Identity) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights connection string |

For Managed Identity with Service Bus, use:

```
ServiceBusConnectionString=Endpoint=sb://<namespace>.servicebus.windows.net/;Authentication=Managed Identity
```

## Message Contract

The Service Bus trigger expects messages with the following structure:

```json
{
  "version": "1.0",
  "jobId": "job-123",
  "documentId": "doc-456",
  "tenantId": "tenant-789",
  "correlationId": "corr-abc",
  "blobContainer": "documents",
  "blobPath": "path/to/document.pdf",
  "idempotencyKey": "unique-key",
  "enqueuedAtUtc": "2025-10-09T10:00:00Z",
  "extractionProfile": "default"
}
```

### Required Fields

- `jobId`: Unique identifier for the background job
- `correlationId`: Correlation ID for distributed tracing

### Validation

Messages are validated for:

- Message version (currently supports "1.0")
- Presence of required fields (jobId, correlationId)
- Valid JSON structure

Invalid messages are dead-lettered with detailed error descriptions.

## Architecture

```
Service Bus Queue (documents.process)
  ↓
DocumentIngestionTrigger
  ↓ (validates & starts orchestration)
DocumentProcessingOrchestrator
  ↓ (coordinates workflow through stages)
Activity Functions:
  Job Lifecycle:
    - StartJob (initializes job processing)
    - GetJob (retrieves job state)
    - GetDocument (retrieves document metadata)
    - CompleteJob (marks job as completed)
    - FailJob (marks job as failed)

  Processing Stages:
    - OcrStageExecutor (OCR and layout extraction)
    - PreprocessStageExecutor (text normalization)
    - EmbedStageExecutor (embedding generation)
    - ExtractStageExecutor (structured data extraction)
    - ValidateStageExecutor (validation and quality checks)
    - PersistStageExecutor (save results to database)
    - NotifyStageExecutor (send completion notifications)

  ManualReview Workflow:
    - RequestManualReview (Processing → ManualReview)
    - ResumeFromManualReview (ManualReview → Processing)
    - RejectManualReview (ManualReview → Failed)
```

## Running Locally

1. **Start Azurite** (local Azure Storage emulator):

   ```bash
   azurite --silent --location ./azurite --debug ./azurite/debug.log
   ```

2. **Run the function**:

   ```bash
   func start
   ```

3. **Send test message** to Service Bus queue `documents.process`

## Building

```bash
dotnet build
```

## Testing

Send a test message to the Service Bus queue:

```bash
# Using Azure CLI
az servicebus queue message send \
  --resource-group <rg> \
  --namespace-name <namespace> \
  --queue-name documents.process \
  --body '{
    "version": "1.0",
    "jobId": "test-job-123",
    "correlationId": "test-corr-456",
    "documentId": "test-doc-789",
    "blobContainer": "test-container",
    "blobPath": "test/document.pdf"
  }'
```

## Implemented Features

- **Service Bus Trigger** - `DocumentIngestionTrigger` validates and processes messages from the queue
- **Durable Orchestration** - `DocumentProcessingOrchestrator` coordinates the document processing workflow through 7 stages
- **Job Lifecycle Management**:
  - `StartJob` - Initializes job processing and updates status to Processing
  - `GetJob` - Retrieves current job state from the database
  - `GetDocument` - Retrieves document metadata from the database
  - `CompleteJob` - Marks job as completed and updates final state
  - `FailJob` - Marks job as failed with error details
- **Processing Stages**:
  - `OcrStageExecutor` - OCR and layout extraction
  - `PreprocessStageExecutor` - Text normalization and preprocessing (✅ Implemented)
  - `EmbedStageExecutor` - Embedding generation
  - `ExtractStageExecutor` - Structured data extraction
  - `ValidateStageExecutor` - Validation and quality checks
  - `PersistStageExecutor` - Save results to database
  - `NotifyStageExecutor` - Send completion notifications
- **ManualReview Workflow** (✅ Implemented):
  - `RequestManualReview` - Pause job for human intervention
  - `ResumeFromManualReview` - Resume processing after review
  - `RejectManualReview` - Reject job during review
  - External event handling via Durable Functions HTTP API
  - Supports RESUME and REJECT decisions
- **Application Insights Integration** - Full telemetry and distributed tracing
- **Message Validation** - Validates message structure and version compatibility
- **Idempotency Support** - Prevents duplicate processing using orchestration instance IDs

## ManualReview Workflow

The orchestrator supports human-in-the-loop workflows where documents can be paused for manual review.

### Flow

1. **Trigger Manual Review**: When a processing stage returns `ErrorCode = "MANUAL_REVIEW_REQUIRED"`, the orchestrator:
   - Calls `RequestManualReview` activity (sets status to ManualReview)
   - Waits for an external event named "ManualReviewDecision"

2. **External Decision**: A human reviewer sends a decision via the Durable Functions HTTP API:
   - **RESUME**: Continues processing from the next stage
   - **REJECT**: Fails the job immediately

3. **Resume or Reject**: Based on the decision:
   - RESUME → Calls `ResumeFromManualReview` (ManualReview → Processing), continues with next stage
   - REJECT → Calls `RejectManualReview` (ManualReview → Failed), orchestration terminates

### Sending ManualReview Decisions

To send a decision to a paused orchestration, use the Durable Functions HTTP API:

```bash
# Get the orchestration instance ID from Application Insights or logs
INSTANCE_ID="<orchestration-instance-id>"

# Send RESUME decision
curl -X POST "http://localhost:7072/runtime/webhooks/durabletask/instances/${INSTANCE_ID}/raiseEvent/ManualReviewDecision" \
  -H "Content-Type: application/json" \
  -d '"RESUME"'

# Send REJECT decision
curl -X POST "http://localhost:7072/runtime/webhooks/durabletask/instances/${INSTANCE_ID}/raiseEvent/ManualReviewDecision" \
  -H "Content-Type: application/json" \
  -d '"REJECT"'
```

**Production Deployment**: Replace `localhost:7072` with your Azure Functions URL:

```
https://<your-function-app>.azurewebsites.net/runtime/webhooks/durabletask/instances/{instanceId}/raiseEvent/ManualReviewDecision
```

### Finding Orchestration Instance IDs

**Option 1 - Application Insights**:

```kusto
traces
| where message contains "Waiting for manual review decision"
| project timestamp, operation_Id, customDimensions.JobId
```

**Option 2 - Durable Functions API**:

```bash
# List all running orchestrations
curl http://localhost:7072/runtime/webhooks/durabletask/instances
```

**Option 3 - Database Query**:

```sql
SELECT JobId, Status, Stage, LastErrorMessage
FROM ProcessJobs
WHERE Status = 'ManualReview'
ORDER BY CompletedAtUtc DESC;
```

### State Transitions

| From | To | Activity | Trigger |
|------|------|----------|---------|
| Processing | ManualReview | `RequestManualReview` | Stage returns MANUAL_REVIEW_REQUIRED |
| ManualReview | Processing | `ResumeFromManualReview` | External event: "RESUME" |
| ManualReview | Failed | `RejectManualReview` | External event: "REJECT" |

### Testing Locally

1. **Trigger a job requiring manual review**:

   ```bash
   # Send a test message that will fail validation (for example)
   az servicebus queue message send \
     --resource-group <rg> \
     --namespace-name <namespace> \
     --queue-name documents.process \
     --body '{"version":"1.0","jobId":"test-123","correlationId":"test-corr"}'
   ```

2. **Check logs for orchestration instance ID**:

   ```bash
   # Look for "Waiting for manual review decision for JobId: test-123"
   ```

3. **Send RESUME decision**:

   ```bash
   curl -X POST "http://localhost:7072/runtime/webhooks/durabletask/instances/{instanceId}/raiseEvent/ManualReviewDecision" \
     -H "Content-Type: application/json" \
     -d '"RESUME"'
   ```

## Next Steps

1. ✅ ~~Implement external event handling for manual review workflows~~ (Completed)
2. Complete implementation of remaining processing stages (OCR, Embed, Extract, Validate, Persist, Notify)
3. Add retry policies and error handling strategies for activity functions
4. Add integration tests for full orchestration workflow
5. Configure monitoring and alerts in Application Insights for ManualReview events
