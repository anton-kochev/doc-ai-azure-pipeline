# Document AI Azure Pipeline (docproc)

A serverless document processing pipeline built with .NET 8.0 Azure Functions and Angular 20.3. The system provides a complete document ingestion, processing, and management solution leveraging Azure cloud services including Blob Storage, Service Bus, SQL Database, and Durable Functions for orchestration.

## Overview

This project implements a microservices architecture for document AI processing with the following components:

- **API Service** (`src/DocProcessing.Api/`) - Azure Functions v4 API with Azure Blob Storage, Service Bus, and Entity Framework Core integration
- **Worker Orchestrator** (`src/worker/orchestrator/DocProcessing.Orchestrator/`) - Azure Durable Functions for document processing orchestration
- **Client Application** (`src/client/receiver-app/`) - Angular 20.3 frontend with Material Design
- **Common Libraries** (`src/common/`)
  - **DocProcessing.Domain** - Entity Framework Core domain models (Document, ProcessJob, ProfileCatalog)
  - **DocProcessing.Contracts** - Shared contracts and DTOs for inter-service communication
  - **DocProcessing.Application** - Application services (DocumentService, ProcessJobService), pipeline stage contracts, and business logic
  - **DocProcessing.Infrastructure** - Infrastructure implementations for storage (BlobStorageService), messaging (ServiceBusService), and database (ApplicationDbContext)
- **Tools**
  - **ServiceBusQueueInspector** - CLI tool for inspecting and monitoring Azure Service Bus queues

### Key Features

- **Serverless Architecture** - Azure Functions for scalable, cost-effective compute
- **Server-Side Upload** - Direct file upload via multipart/form-data with validation
- **Asynchronous Processing** - Service Bus queue-based document processing
- **Durable Orchestration** - Stateful workflows with Azure Durable Functions
- **Clean Architecture** - Domain-driven design with clear separation of concerns (Domain, Application, Infrastructure)
- **Entity Framework Core** - Type-safe data access with SQL Server and automatic migrations
- **Managed Identity** - Secure Azure service authentication without connection strings
- **Idempotency** - SHA256 hash-based deduplication prevents duplicate processing
- **Retry Mechanism** - Failed jobs can be retried via API endpoint
- **Pipeline Stages** - Standardized stage contracts for document processing workflow
- **Material Design UI** - Modern Angular application with light/dark mode support
- **Local Development** - Full emulator support (Azurite, Service Bus)
- **CI/CD Pipeline** - Automated deployment via GitHub Actions
- **Monitoring Tools** - ServiceBusQueueInspector CLI for queue inspection

### How It Works

1. **Document Upload**
   - User uploads document via Angular client (multipart/form-data)
   - API validates file type, size, and calculates SHA256 hash
   - Document uploaded to Azure Blob Storage using Managed Identity
   - Document and ProcessJob records created in SQL Server (idempotent based on SHA256)
   - Job message enqueued to Service Bus for processing

2. **Document Processing**
   - Worker Orchestrator (Durable Functions) picks up message from Service Bus
   - Document processing orchestrated through standardized pipeline stages
   - Each stage (OCR, Preprocess, Embed, Extract, Validate, Persist, Notify) tracked in ProcessJob
   - Job status updated in SQL Server (Pending → Processing → Completed/Failed/ManualReview)

3. **Status Tracking & Retry**
   - ProcessJob entity tracks processing status and current stage
   - Failed jobs can be retried via `/api/jobs/{jobId}/retry` endpoint
   - Profile catalog manages extraction profiles
   - Application Insights provides telemetry and distributed tracing

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) (LTS version)
- [Docker](https://www.docker.com/get-started) and Docker Compose (for local emulators)
- Azure Storage Account (for blob storage features, or use local Azurite emulator)
- Azure Service Bus (for message queue features, or use local emulator)

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd doc-ai-azure-pipeline/docproc
```

### 2. Local Development with Emulators (Recommended)

For local development, you can use Docker containers to run Azure Storage (Azurite) and Azure Service Bus emulators:

#### Start the Emulators

```bash
# From the repository root
docker-compose up -d
```

This starts:
- **Azurite** - Azure Storage emulator (Blob, Queue, Table services)
  - Blob service: `http://localhost:10000`
  - Queue service: `http://localhost:10001`
  - Table service: `http://localhost:10002`
- **Service Bus Emulator** - Azure Service Bus emulator
  - AMQP port: `5672`
  - Management port: `5300`

#### Configure the Emulator Connection Strings

The `appsettings.Development.json` file is already configured with local emulator connection strings:

```json
{
  "AzureStorage": {
    "ConnectionString": "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://127.0.0.1",
    "ContainerName": "uploads"
  },
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "QueueName": "documents.process"
  }
}
```

**Note:** The Service Bus emulator connection string format is specifically for native local development. When the emulator container and your application are running natively on the local machine, use this exact connection string format.

#### Service Bus Queue Configuration

The Service Bus emulator is configured via `config.json` in the repository root. The `documents.process` queue is pre-configured with the following properties:

- **Dead-lettering on message expiration**: Enabled
- **Message TTL**: 1 day
- **Lock duration**: 5 minutes
- **Max delivery count**: 10

To modify queue configuration or add new queues/topics, edit `config.json` and restart the emulator.

#### Stop the Emulators

```bash
docker-compose down
```

### 3. Configure Azure Services (Production/Cloud)

For non-local environments, create or update `src/api/appsettings.json`:

```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account-name",
    "ContainerName": "your-container-name"
  },
  "ServiceBus": {
    "ConnectionString": "your-service-bus-connection-string",
    "Namespace": "your-namespace.servicebus.windows.net",
    "QueueName": "documents.process"
  }
}
```

### 4. Run the API

```bash
# From the docproc directory
cd docproc

# Build the solution
dotnet build docproc.sln

# Run the API with Azure Functions runtime
cd src/DocProcessing.Api
func start

# Or run with hot reload using dotnet watch (from docproc directory)
dotnet watch --project src/DocProcessing.Api/DocProcessing.Api.csproj
```

The API will be available at:
- HTTP: `http://localhost:7071`
- API endpoints: `http://localhost:7071/api/*`

### 5a. Run the Worker Orchestrator

```bash
cd src/worker/orchestrator/DocProcessing.Orchestrator
func start
```

### 6. Run the Angular Client

```bash
cd src/client/receiver-app
npm install
npm start
```

The client will be available at `http://localhost:4200`

## Project Structure

```
docproc/
├── src/
│   ├── DocProcessing.Api/           # Azure Functions v4 API
│   │   ├── Functions/               # Azure Functions (UploadFunctions with Upload and RetryJob endpoints)
│   │   ├── Program.cs               # Application entry point with DI configuration
│   │   ├── host.json                # Azure Functions host configuration
│   │   └── Dockerfile               # Docker configuration
│   ├── DocProcessing.Api.Tests/     # Unit tests for API
│   │   └── Services/                # Service layer tests
│   ├── common/                      # Shared libraries (Clean Architecture layers)
│   │   ├── DocProcessing.Domain/    # Domain entities (Document, ProcessJob, ProfileCatalog)
│   │   ├── DocProcessing.Contracts/ # Shared contracts (ProcessDocumentMessage)
│   │   ├── DocProcessing.Application/ # Application services and business logic
│   │   │   ├── Services/            # DocumentService, ProcessJobService
│   │   │   ├── Pipeline/            # Pipeline stage contracts (IJobStageActivity)
│   │   │   ├── Interfaces/          # Service interfaces
│   │   │   └── Validation/          # Validation logic
│   │   └── DocProcessing.Infrastructure/ # Infrastructure implementations
│   │       ├── Storage/             # BlobStorageService with Managed Identity
│   │       ├── MessageBroker/       # ServiceBusService with Managed Identity
│   │       ├── FileUpload/          # File upload configuration and options
│   │       ├── ApplicationDbContext.cs # EF Core DbContext
│   │       └── Migrations/          # EF Core migrations
│   ├── worker/
│   │   └── orchestrator/
│   │       └── DocProcessing.Orchestrator/ # Azure Durable Functions orchestrator
│   │           └── Functions/       # Durable Functions for workflow orchestration
│   └── client/
│       └── receiver-app/            # Angular 20.3 application
├── ServiceBusQueueInspector/        # CLI tool for Service Bus queue inspection
├── ServiceBusQueueInspector.Tests/  # Tests for the inspector tool
├── docs/                            # Documentation
├── infra/                           # Infrastructure as Code
├── knowledge-base/                  # Project knowledge base (v1.1.0)
│   ├── core.md                     # KB index and versioning
│   ├── README.md                   # KB overview
│   ├── CONTRIBUTING.md             # KB contribution guide
│   ├── code-style/                 # Style guides by framework
│   │   ├── dotnet/                 # C#/.NET guides
│   │   ├── angular/                # Angular guides
│   │   └── typescript/             # TypeScript guides
│   ├── recipes/                    # Reusable patterns
│   ├── tools/                      # KB transformation tools
│   └── tests/                      # KB validation
├── .github/
│   └── workflows/                   # CI/CD pipelines
│       ├── deploy-api-functions.yml              # Deploy API to Azure Functions
│       ├── deploy-worker-orchestrator-functions.yml # Deploy orchestrator worker
│       └── azure-static-web-apps-receiver.yml    # Deploy Angular client
├── docproc.sln                     # Solution file
└── global.json                     # .NET SDK version
```

## Development

### API Development

```bash
# From the docproc directory
cd docproc

# Build in Release mode
dotnet build docproc.sln -c Release

# Run API without watch
dotnet run --project src/DocProcessing.Api/DocProcessing.Api.csproj

# Run Worker Orchestrator
cd src/worker/orchestrator/DocProcessing.Orchestrator
func start

# Run tests
dotnet test

# Run specific test project
dotnet test src/DocProcessing.Api.Tests/DocProcessing.Api.Tests.csproj

# Run ServiceBusQueueInspector tool
dotnet run --project ServiceBusQueueInspector/ServiceBusQueueInspector.csproj
```

### Client Development

```bash
cd src/client/receiver-app

# Start development server
npm start

# Build for production
npm run build

# Run tests
npm test

# Run tests with coverage
npm test -- --code-coverage

# Lint and fix
npm run lint

# Format code
npm run format
```

### Docker

```bash
# Build API Docker image (from docproc directory)
cd docproc
docker build -f src/DocProcessing.Api/Dockerfile -t docproc-api .

# Run API container
docker run -p 7071:7071 docproc-api

# Run local emulators (Azurite + Service Bus)
docker-compose up -d

# Stop emulators
docker-compose down
```

## API Endpoints

The API is built with Azure Functions and provides the following endpoints:

### Document Upload

- `POST /api/upload` - Server-side file upload with validation and idempotency
  - **Content-Type**: `multipart/form-data`
  - **Form fields**:
    - `file` (required) - The document file to upload
    - `extractionProfile` (optional) - Profile name for document extraction (e.g., "invoice", "contract")
    - `tenantId` (optional) - GUID for multi-tenant scenarios
  - **Validation**: File type, size, and SHA256 hash calculation
  - **Response** (202 Accepted):
    ```json
    {
      "jobId": "guid",
      "documentId": "guid",
      "isNewJob": true,
      "isNewDocument": true,
      "extractionProfile": "invoice",
      "blobUrl": "https://...",
      "fileName": "document.pdf",
      "contentType": "application/pdf",
      "fileSizeBytes": 123456
    }
    ```

### Job Management

- `POST /api/jobs/{jobId}/retry` - Retry a failed job
  - **Path parameter**: `jobId` (GUID)
  - **Response** (200 OK):
    ```json
    {
      "message": "Job re-queued for retry",
      "jobId": "guid"
    }
    ```
  - **Error responses**:
    - 400 Bad Request - Invalid job ID format
    - 404 Not Found - Job not found or not in Failed status

The API integrates with:
- **Azure Blob Storage** - Document storage with Managed Identity authentication
- **Azure Service Bus** - Asynchronous message processing with Managed Identity
- **SQL Server** - Metadata persistence via Entity Framework Core with automatic migrations
- **Application Insights** - Distributed tracing and telemetry

## Technology Stack

### Backend
- **.NET 8.0** - Target framework for all services
- **Azure Functions v4** - Serverless compute platform for API and orchestration
- **Azure Durable Functions** - Stateful orchestration workflows
- **Entity Framework Core 9.0** - ORM for data access with SQL Server
- **Azure Storage Blobs SDK** - Document storage
- **Azure Service Bus SDK** - Asynchronous messaging
- **Azure Identity** - Authentication and authorization

### Frontend
- Angular 20.3
- Angular Material 20.2.7
- RxJS 7.8
- TypeScript 5.9
- Karma & Jasmine (testing)
- ESLint & Prettier (code quality)

### Architecture & Patterns
- **Clean Architecture** - Separation of concerns with Domain, Application, and Infrastructure layers
- **Domain-Driven Design** - Domain entities with EF Core for persistence
- **Pipeline Pattern** - Standardized stage contracts (IJobStageActivity) for document processing workflow
- **Message-Based Architecture** - Asynchronous processing via Service Bus
- **Idempotency** - SHA256 hash-based deduplication for documents and jobs
- **Retry Pattern** - Failed jobs can be retried via API endpoint
- **Managed Identity** - Secure Azure service authentication without connection strings

### Infrastructure
- **Docker** (Linux containers)
- **Azure Blob Storage** - Document storage
- **Azure Service Bus** - Message queue for document processing
- **Azure SQL Server** - Relational database for metadata
- **Azurite** - Azure Storage emulator for local development
- **Azure Service Bus Emulator** - Local message queue for development

## Configuration

### API Configuration (`appsettings.json`)

The API uses Azure Functions configuration with Managed Identity support. Key settings include:

```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account",
    "ContainerName": "uploads",
    "UseManagedIdentity": true
  },
  "ServiceBus": {
    "FullyQualifiedNamespace": "your-namespace.servicebus.windows.net",
    "QueueName": "documents.process",
    "UseManagedIdentity": true
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=DocProcessing;..."
  },
  "FileUpload": {
    "MaxFileSizeMB": 10,
    "AllowedFileTypes": ["application/pdf", "image/jpeg", "image/png"]
  },
  "Database": {
    "AutoApplyMigrations": true
  },
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=..."
  }
}
```

**Key Configuration Sections:**
- **AzureStorage** - Blob storage with Managed Identity (or ConnectionString for local dev)
- **ServiceBus** - Message queue with Managed Identity (or ConnectionString for local dev)
- **FileUpload** - File validation settings (max size, allowed types)
- **Database** - EF Core migration settings
- **ApplicationInsights** - Telemetry and logging

**Note:** For local development, use `local.settings.json` (not committed to source control) or `appsettings.Development.json` with connection strings for emulators.

### Entity Framework Migrations

```bash
# Add new migration
cd docproc/src/DocProcessing.Api
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Remove last migration
dotnet ef migrations remove
```

### Angular Configuration

The Angular app is configured with:
- SCSS for styling
- Material Design theming (light/dark modes)
- Prettier with 100-character line width
- ESLint with Angular-specific rules
- Karma for unit testing

## Knowledge Base

This project includes a comprehensive knowledge base (v1.1.0) for code style guides, best practices, and reusable patterns:

- **Location**: `docproc/knowledge-base/`
- **Index**: `knowledge-base/core.md`
- **Overview**: `knowledge-base/README.md`
- **Contributing**: See `knowledge-base/CONTRIBUTING.md`

### Included Content

#### Code Style Guides
- **.NET/C#**
- **Angular**
- **TypeScript**
- **Recipes**

### Claude AI Integration

The knowledge base is integrated with Claude Code for AI-assisted development:

**CLAUDE.md** has a reference to the knowledge base

**Generated prompt** at `knowledge-base/tools/adapters/claude/claude-agent-prompt.md`

**npm run generate** command to regenerate when KB changes

#### How to Use It

When you need KB guidelines:

```
Follow the Angular guidelines from knowledge-base/tools/adapters/claude/claude-agent-prompt.md
```

or

```
Read the code style guides in knowledge-base/tools/adapters/claude/claude-agent-prompt.md
and help me build a component
```

Claude Code will read the file only when requested, keeping token usage minimal.

#### Regenerating the Knowledge Base

Whenever you update the knowledge base content, run:

```bash
cd docproc/knowledge-base
npm run generate
```

This will regenerate the Claude AI agent configuration and prompt artifacts.

## Contributing

1. Follow the coding standards defined in the knowledge base
2. Use Prettier for code formatting
3. Ensure all tests pass before submitting PRs
4. Update documentation for new features

## License

[Add license information]

## CI/CD Pipeline

The project uses GitHub Actions for continuous integration and deployment:

### Workflows

1. **deploy-api-functions.yml** - Deploys the API to Azure Functions
   - Triggers on pushes to `main` branch affecting API code
   - Builds and publishes the DocProcessing.Api project
   - Deploys to Azure Functions app

2. **deploy-worker-orchestrator-functions.yml** - Deploys the orchestrator worker
   - Triggers on pushes to `main` branch affecting orchestrator code
   - Builds and publishes the Worker.Orchestrator project
   - Deploys to Azure Functions app

3. **azure-static-web-apps-receiver.yml** - Deploys the Angular client
   - Triggers on pushes to `main` branch affecting client code
   - Builds the Angular application
   - Deploys to Azure Static Web Apps

## Recent Updates

### Latest (October 2024)
- ✅ **Infrastructure Layer** - Refactored to shared Infrastructure layer with BlobStorageService and ServiceBusService
- ✅ **Managed Identity** - Added Managed Identity support for Azure Storage and Service Bus authentication
- ✅ **Retry Mechanism** - Implemented RetryJob API endpoint for failed job recovery
- ✅ **Pipeline Stages** - Added standardized pipeline stage contracts (IJobStageActivity) and execution context
- ✅ **Application Services** - Moved DocumentService and ProcessJobService to Application layer
- ✅ **Idempotency** - SHA256 hash-based deduplication for documents and jobs
- ✅ **Database Migrations** - Automatic EF Core migrations with configurable auto-apply

### Previous Updates
- ✅ **Clean Architecture** - Implemented Domain, Contracts, Application, and Infrastructure layers
- ✅ **Azure Functions v4** - Migrated API to serverless Azure Functions (.NET 8 isolated)
- ✅ **Azure Durable Functions** - Added orchestration worker (DocProcessing.Orchestrator)
- ✅ **Entity Framework Core 9.0** - SQL Server integration with migrations
- ✅ **CI/CD Pipelines** - GitHub Actions workflows for automated deployment
- ✅ **ServiceBusQueueInspector** - CLI tool for queue monitoring
- ✅ **Unit Tests** - Test coverage for API services
- ✅ **Local Development** - Azure Service Bus emulator and Azurite integration
- ✅ **Knowledge Base** - Comprehensive .NET/C# code style guides (v1.1.0)
- ✅ **Angular Client** - Material Design with light/dark themes (v20.3)
