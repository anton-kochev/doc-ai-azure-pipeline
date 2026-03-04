# Development Workflows

## 1. Adding a New Domain Entity

When adding a new domain entity to the project:

1. Create the entity class in `src/common/DocProcessing.Domain/Entities/`
   - Follow existing entity patterns (e.g., `Document.cs`, `ProcessJob.cs`)
   - Include audit fields if needed (CreatedAt, UpdatedAt, etc.)
   - Add navigation properties for relationships

2. Add DbSet to `DocProcessingDbContext` in `src/DocProcessing.Api/Data/`

   ```csharp
   public DbSet<YourEntity> YourEntities { get; set; }
   ```

3. Configure entity in DbContext `OnModelCreating` if needed (indexes, constraints, etc.)

4. Create and apply migration:

   ```bash
   cd src/DocProcessing.Api
   dotnet ef migrations add Add_YourEntity --project . --startup-project .
   dotnet ef database update
   ```

5. Respect Clean Architecture dependencies:
   - Domain entities should have no dependencies
   - Application layer references Domain
   - API/Infrastructure references Application and Domain

## 2. Adding a New API Endpoint

To add a new Azure Function endpoint:

1. Create a new Function class in `src/DocProcessing.Api/Functions/`
   - Use dependency injection for services
   - Follow naming convention: `{Resource}{Action}Function.cs`

2. Add the Function trigger and bindings:

   ```csharp
   [Function("YourFunctionName")]
   public async Task<IActionResult> Run(
       [HttpTrigger(AuthorizationLevel.Function, "post", Route = "your-route")]
       HttpRequest req)
   ```

3. Implement validation:
   - Validate input parameters
   - Return appropriate status codes (400 for validation errors)
   - Use model binding where appropriate

4. Integrate with Application services:
   - Inject services via constructor
   - Keep Functions thin - delegate logic to Application layer
   - Handle exceptions and return proper error responses

5. Add error handling and logging:
   - Use `ILogger<T>` for logging
   - Log at appropriate levels (Information, Warning, Error)
   - Include correlation IDs for tracing

6. Test the endpoint:
   - Add unit tests in `src/DocProcessing.Api.Tests/`
   - Test validation, success cases, and error handling
   - Run locally with `func start` to verify

## 3. Extending the ProcessJob State Machine

To add new states or stages to the ProcessJob workflow:

1. Update the enum in `src/common/DocProcessing.Domain/Entities/ProcessJob.cs`:
   - Add new `JobStatus` values (Pending, Processing, Completed, Failed, ManualReview)
   - Add new `ProcessingStage` values (Uploaded, OCR, Preprocess, Embed, Extract, Validate, Persist, Notify)

2. Update state transition logic:
   - Modify `ProcessJob.CanTransitionTo()` if state machine rules change
   - Update `UpdateStatus()` and `UpdateStage()` methods if needed
   - Ensure state transitions are valid and idempotent

3. Handle new states in orchestrator:
   - Update `src/worker/orchestrator/Worker.Orchestrator/` to handle new stages
   - Add or modify activity functions for new processing steps
   - Update orchestration workflow to include new stages

4. Create database migration:

   ```bash
   cd src/DocProcessing.Api
   dotnet ef migrations add Update_ProcessJob_States --project . --startup-project .
   dotnet ef database update
   ```

5. Update any UI or API responses that expose job status/stages

6. Test state transitions thoroughly:
   - Test all valid transitions
   - Verify invalid transitions are rejected
   - Test retry scenarios for failed jobs

## 4. Adding a New Service Bus Message Type

To add a new message contract and handler:

1. Create message contract in `src/common/DocProcessing.Contracts/Messages/`
   - Follow naming convention: `{Action}{Resource}Message.cs`
   - Include all necessary data for processing
   - Make properties immutable where possible

2. Add message handler:
   - For API: Create handler in `src/DocProcessing.Api/MessageHandlers/`
   - For Worker: Create activity function in orchestrator
   - Use dependency injection for services

3. Register handler in DI container:
   - Update `Program.cs` or startup configuration
   - Ensure handler is registered with correct lifetime (usually scoped)

4. Configure queue/topic if new:
   - Update infrastructure configuration in `infra/`
   - Add queue name to `appsettings.json` and `local.settings.json`
   - Ensure Service Bus namespace has the queue created

5. Implement message publishing:
   - Use `ServiceBusClient` with Managed Identity
   - Include proper error handling and retries
   - Log message sending for traceability

6. Test message flow:
   - Test message serialization/deserialization
   - Verify message is received and processed correctly
   - Test error scenarios and dead-lettering

## 5. Adding a New Orchestration Stage

To add a new stage to the document processing pipeline:

1. Create stage executor in `src/worker/orchestrator/Worker.Orchestrator/Executors/`
   - Implement common executor interface or pattern
   - Follow naming: `{StageName}Executor.cs`
   - Inject required services via constructor

2. Implement stage logic:
   - Handle the specific processing for this stage
   - Update ProcessJob status and stage appropriately
   - Handle errors and set job to Failed status if needed
   - Log progress and any issues

3. Register in DI container:
   - Update `Program.cs` in orchestrator project
   - Register executor and any dependencies
   - Ensure correct service lifetime (usually scoped)

4. Update orchestration workflow:
   - Modify orchestrator function to call new stage
   - Ensure proper sequencing with other stages
   - Handle stage-specific error conditions

5. Update pipeline configuration:
   - Add stage to ProcessingStage enum if not already present
   - Update any configuration files if stage needs settings
   - Document stage purpose and behavior

6. Test the stage:
   - Unit test executor logic
   - Integration test with full pipeline
   - Test error handling and retry scenarios
   - Verify telemetry and logging work correctly

7. Update E2E tests:
   - Add the new stage to `PipelineSimulator.Stages` in `tests/DocProcessing.EndToEnd.Tests/Helpers/PipelineSimulator.cs` if not already present
   - Verify the guard test in `HappyPathFlowTests.PipelineSimulatorStages_MatchOrchestratorStageSequence` still passes
   - Add stage-specific test scenarios in `Flows/` if the stage has meaningful logic (use `ControllableActivityFactory` to inject failures)

## 6. Local Development Setup

First-time setup for local development:

1. **Install Prerequisites**:
   - .NET 8.0 SDK
   - Azure Functions Core Tools v4
   - Node.js 18+ (for Angular client)
   - Docker Desktop (optional, for containerized dependencies)
   - Azure Storage Explorer or Azurite (for local storage)

2. **Clone and Build**:

   ```bash
   git clone <repo-url>
   cd doc-ai-azure-pipeline
   dotnet build docproc.sln
   ```

3. **Setup Local Azure Services**:

   Option A - Use Azurite (Local Azure Storage Emulator):

   ```bash
   # Install Azurite
   npm install -g azurite

   # Start Azurite
   azurite --silent --location ./azurite --debug ./azurite/debug.log
   ```

   Option B - Use Docker Compose:

   ```bash
   docker-compose up -d
   ```

4. **Configure API Functions**:
   - Copy `src/DocProcessing.Api/local.settings.json.example` to `local.settings.json`
   - Update connection strings:

     ```json
     {
       "IsEncrypted": false,
       "Values": {
         "AzureWebJobsStorage": "UseDevelopmentStorage=true",
         "AzureStorage__ConnectionString": "UseDevelopmentStorage=true",
         "ServiceBus__ConnectionString": "<local-or-dev-service-bus>",
         "Database__ConnectionString": "<local-sql-connection>",
         "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
       }
     }
     ```

5. **Setup Database**:

   ```bash
   cd src/DocProcessing.Api
   dotnet ef database update
   ```

6. **Configure Worker Orchestrator**:
   - Copy `src/worker/orchestrator/Worker.Orchestrator/local.settings.json.example` to `local.settings.json`
   - Use same connection strings as API

7. **Run All Services**:

   Terminal 1 - API Functions:

   ```bash
   cd src/DocProcessing.Api
   func start --port 7071
   ```

   Terminal 2 - Worker Orchestrator:

   ```bash
   cd src/worker/orchestrator/Worker.Orchestrator
   func start --port 7072
   ```

   Terminal 3 - Angular Client:

   ```bash
   cd src/client/receiver-app
   npm install
   npm start
   ```

8. **Verify Setup**:
   - API Functions: http://localhost:7071
   - Worker Functions: http://localhost:7072
   - Angular Client: http://localhost:4200
   - Test file upload through UI or API

## 7. Debugging End-to-End Flows

To debug the complete document processing flow:

1. **Upload -> Queue Flow**:
   - Set breakpoint in `UploadFunction` in API
   - Upload file via Angular client or POST to `/api/upload`
   - Verify document saved to Blob Storage
   - Check Service Bus queue for message
   - Verify ProcessJob created in database with status=Pending

2. **Queue -> Processing Flow**:
   - Set breakpoint in orchestrator trigger function
   - Verify message is picked up from Service Bus
   - Step through orchestration activities
   - Check ProcessJob status transitions (Pending -> Processing)
   - Verify stage progression (Uploaded -> OCR -> Preprocess, etc.)

3. **Using Application Insights Locally**:
   - Add Application Insights connection string to local.settings.json
   - Use correlation IDs to trace requests across services
   - Query logs in Azure Portal or use Application Insights SDK locally
   - Enable verbose logging in host.json for detailed traces

4. **Troubleshooting Failed Jobs**:
   - Query database for jobs with status=Failed
   - Check ErrorMessage and ErrorDetails fields on ProcessJob
   - Review Application Insights logs for exceptions
   - Retry failed job via API: `POST /api/jobs/{jobId}/retry`

5. **Common Issues**:
   - **Connection failures**: Verify connection strings in local.settings.json
   - **Managed Identity errors**: Use connection strings locally instead of MI
   - **Database migrations**: Ensure migrations are applied with `dotnet ef database update`
   - **Service Bus not processing**: Check queue exists and permissions are correct
   - **Blob storage errors**: Verify Azurite is running or connection string is correct

6. **Logging Best Practices**:
   - Use structured logging with `ILogger<T>`
   - Include job ID and document ID in all log statements
   - Log state transitions and stage changes
   - Log before and after external service calls
   - Use appropriate log levels (Debug/Information/Warning/Error)

7. **Running E2E Integration Tests**:
   - Run `dotnet test --project tests/DocProcessing.EndToEnd.Tests/` to verify pipeline flow
   - Tests use real Application services with in-memory DB and mocked Azure services
   - Use `ControllableActivityFactory` to inject failures at specific stages
   - `PipelineSimulator` mirrors the orchestrator's stage sequence with metadata forwarding
   - See `tests/README.md` for full architecture and test class details
