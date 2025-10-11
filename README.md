# Document AI Azure Pipeline (docproc)

A document processing pipeline built with .NET 8.0 and Angular 20.3, featuring Azure Blob Storage integration for file management and processing.

## Overview

This project implements a microservices architecture for document AI processing with the following components:

- **API Service** (`src/api/`) - ASP.NET Core 8.0 Web API with Azure Blob Storage integration
- **Client Application** (`src/client/receiver-app/`) - Angular 20.3 frontend with Material Design
- **Worker Service** (`src/worker/`) - Background processing service (planned)
- **Common Library** (`src/common/`) - Shared code and utilities (planned)

### Key Features

- File upload with client-side Azure SAS-based uploads
- Azure Blob Storage integration for document management
- OpenAPI/Swagger documentation
- Material Design theming with light/dark mode support
- Dependency injection guidance and patterns

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
# Build the solution
dotnet build docproc.sln

# Run the API with hot reload
dotnet watch --project src/api/api.csproj
```

The API will be available at:
- HTTP: `http://localhost:8080`
- HTTPS: `https://localhost:8081`
- Swagger UI: `http://localhost:8080/swagger` (in Development mode)

### 5. Run the Angular Client

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
│   ├── api/                    # ASP.NET Core Web API
│   │   ├── Configuration/      # Configuration models
│   │   ├── Endpoints/          # API endpoints
│   │   ├── Services/           # Business logic services
│   │   ├── Program.cs          # Application entry point
│   │   └── Dockerfile          # Docker configuration
│   ├── client/
│   │   └── receiver-app/       # Angular application
│   ├── common/                 # Shared library (planned)
│   └── worker/                 # Background worker (planned)
├── tests/                      # Test projects
├── docs/                       # Documentation
├── infra/                      # Infrastructure as Code
├── knowledge-base/             # Project knowledge base (v1.1.0)
│   ├── core.md                # KB index and versioning
│   ├── README.md              # KB overview
│   ├── CONTRIBUTING.md        # KB contribution guide
│   ├── code-style/            # Style guides by framework
│   │   ├── dotnet/            # C#/.NET guides
│   │   ├── angular/           # Angular guides
│   │   └── typescript/        # TypeScript guides
│   ├── recipes/               # Reusable patterns
│   ├── tools/                 # KB transformation tools
│   └── tests/                 # KB validation
├── docproc.sln                # Solution file
└── global.json                # .NET SDK version
```

## Development

### API Development

```bash
# Build in Release mode
dotnet build docproc.sln -c Release

# Run without watch
dotnet run --project src/api/api.csproj

# Run tests (when available)
dotnet test
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
# Build Docker image (from docproc directory)
docker build -f src/api/Dockerfile -t docproc-api .

# Run container
docker run -p 8080:8080 -p 8081:8081 docproc-api
```

## API Endpoints

### Upload Endpoints

- `POST /api/upload/sas` - Generate SAS URL for direct client-side upload
  - Request: `{ "fileName": "string" }`
  - Response: `{ "sasUrl": "string", "blobUrl": "string" }`

- `POST /api/upload` - Server-side file upload
  - Content-Type: `multipart/form-data`
  - Form field: `file`
  - Response: `{ "blobUrl": "string", "fileName": "string" }`

Full API documentation is available at `/swagger` when running in Development mode.

## Technology Stack

### Backend
- .NET 8.0
- ASP.NET Core Minimal APIs
- Azure Storage Blobs SDK
- Swashbuckle (OpenAPI/Swagger)

### Frontend
- Angular 20.3
- Angular Material 20.2.7
- RxJS 7.8
- TypeScript 5.9
- Karma & Jasmine (testing)
- ESLint & Prettier (code quality)

### Infrastructure
- Docker (Linux containers)
- Azure Blob Storage
- Azure Service Bus
- Azurite (Azure Storage emulator)
- Azure Service Bus Emulator

## Configuration

### API Configuration (`appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureStorage": {
    "ConnectionString": "",
    "ContainerName": ""
  },
  "ServiceBus": {
    "ConnectionString": "",
    "Namespace": "",
    "QueueName": "documents.process"
  }
}
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

## Recent Updates

- ✅ Azure Service Bus emulator support for local development
- ✅ Azurite (Azure Storage emulator) integration with docker-compose
- ✅ Knowledge Base with comprehensive .NET/C# code style guides
- ✅ Client-side file upload with Azure SAS-based uploads
- ✅ Azure Blob Storage support with SAS URL generation
- ✅ Material theming with light/dark themes
- ✅ OpenAPI/Swagger documentation
- ✅ Dependency injection guidance
