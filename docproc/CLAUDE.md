# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is a document AI pipeline project ("docproc") built with .NET 8.0. The solution is organized with a microservices architecture:

- `src/api/` - ASP.NET Core 8.0 Web API (currently contains a minimal "Hello World" endpoint)
- `src/worker/` - Background worker service (not yet implemented)
- `src/client/` - Client application (not yet implemented)
- `src/common/` - Shared code library (not yet implemented)
- `docs/` - Documentation (empty)
- `infra/` - Infrastructure configuration (empty)
- `tests/` - Test projects (empty)

The project is containerized with Docker support targeting Linux containers.

## Commands

### Build and Run

```bash
# Build the solution
dotnet build docproc.sln

# Build specific configuration
dotnet build docproc.sln -c Release

# Run the API project
dotnet run --project src/api/api.csproj

# Run with watch mode (hot reload)
dotnet watch --project src/api/api.csproj
```

### Docker

```bash
# Build Docker image (from repository root)
docker build -f src/api/Dockerfile -t docproc-api .

# Run container
docker run -p 8080:8080 -p 8081:8081 docproc-api
```

### Testing

```bash
# Run all tests (when test projects are added)
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Architecture Notes

- **Target Framework**: .NET 8.0 with nullable reference types enabled
- **Docker**: Multi-stage build with separate base, build, publish, and final stages using Microsoft's official .NET images
- **Launch Settings**: API configured to listen on ports 8080 (HTTP) and 8081 (HTTPS)
- **Structure**: The solution currently has only the `api` project implemented; other services (worker, client, common) have placeholder directories

## Configuration

- `global.json` enforces .NET SDK 8.0.0 with `latestMinor` roll-forward policy
- `appsettings.json` and `appsettings.Development.json` in the API project control logging and runtime behavior
