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
  ↓ (coordinates workflow)
Activity Functions:
  - StartJob (initializes job processing)
  - GetJob (retrieves job state)
  - CompleteJob (marks job as completed)
  - FailJob (marks job as failed)
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
- **Durable Orchestration** - `DocumentProcessingOrchestrator` coordinates the document processing workflow
- **Activity Functions**:
  - `StartJob` - Initializes job processing and updates status to Processing
  - `GetJob` - Retrieves current job state from the database
  - `CompleteJob` - Marks job as completed and updates final state
  - `FailJob` - Marks job as failed with error details
- **Application Insights Integration** - Full telemetry and distributed tracing
- **Message Validation** - Validates message structure and version compatibility
- **Idempotency Support** - Prevents duplicate processing using orchestration instance IDs

## Next Steps

1. Add retry policies and error handling strategies for activity functions
2. Implement document processing stages (OCR, Preprocess, Embed, Extract, Validate, Persist, Notify)
3. Add unit and integration tests
4. Configure monitoring and alerts in Application Insights
5. Implement external event handling for manual review workflows
