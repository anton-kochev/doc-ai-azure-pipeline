# Universal Knowledge Base

**Version**: 1.1.0
**Last Updated**: 2025-10-06

## Overview

This is a modular, token-efficient Knowledge Base designed for both human developers and AI agents. It provides a canonical source of truth for code style guides, architectural patterns, recipes, and best practices for the project.

## Philosophy

- **Single Source of Truth**: One KB, multiple consumers (humans, Claude, Cursor, etc.)
- **Token-Efficient**: Structured for AI context windows with minimal redundancy
- **Modular**: Organized by domain with clear separation of concerns
- **Machine-Readable**: JSON schemas + YAML frontmatter for automation
- **Version-Controlled**: All changes tracked, reviewed, and tested

## Included Content

### Code Style Guides

#### .NET/C# (v1.1.0)

- **[C# Core](code-style/dotnet/csharp-core.md)** - Modern C# 12+ language features
  - Nullable reference types
  - Records and init-only properties
  - Pattern matching and switch expressions
  - File-scoped namespaces
  - Required members
  - Async/await best practices
  - LINQ patterns
  - **Explicit typing** (no `var`)

- **[.NET Core](code-style/dotnet/dotnet-core.md)** - .NET 8+ framework patterns
  - Dependency injection and service lifetimes
  - Configuration with Options pattern
  - Structured logging with source generators
  - Entity Framework Core patterns
  - HttpClient with Polly resilience
  - FluentValidation
  - Background services
  - Result pattern for error handling

- **[ASP.NET Core](code-style/dotnet/aspnet-core.md)** - Web API development
  - Modern Program.cs with minimal hosting
  - Controller patterns and minimal APIs
  - Request/response DTOs with records
  - Model validation
  - Global exception handling
  - API versioning
  - JWT authentication and authorization
  - CORS, output caching, response compression
  - Health checks and rate limiting
  - OpenAPI/Swagger configuration

#### Angular/TypeScript

- **[Angular Core](code-style/angular/angular-core.md)** - Angular 14+ patterns
  - Component structure and lifecycle
  - Dependency injection with `inject()` function
  - RxJS patterns
  - BEM methodology for CSS

- **[Angular Components](code-style/angular/components.md)** - Component guidelines
  - OnPush change detection
  - Signals for reactive state
  - Input/output patterns
  - BEM CSS structure examples

- **[TypeScript Core](code-style/typescript/typescript-core.md)** - TypeScript best practices
  - Strict mode configuration
  - Type system patterns
  - Naming conventions
  - Async/await patterns

## Usage

### For Humans

Browse the KB directly via Markdown files. Start with [core.md](core.md) for the complete index.

### For AI Agents

#### Generate Claude Code Knowledge Base

```bash
npm install
npm run generate
```

This generates:

- `tools/adapters/claude/claude-kb.json` - Structured JSON configuration
- `tools/adapters/claude/claude-agent-prompt.md` - Formatted markdown prompt

Current stats:
- **Total entries**: 7
- **Estimated tokens**: ~21,000

#### Available Adapters
- **Claude**: `tools/adapters/claude/generator.js`
- **Cursor**: `tools/adapters/cursor/generator.js`

### Contributing
See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines and review process.

## Structure

```
knowledge-base/
├── core.md                      # Central index and versioning
├── README.md                    # This file
├── CONTRIBUTING.md              # Contribution guidelines
├── OWNERS                       # Code owners
├── package.json                 # Node dependencies
│
├── schemas/                     # JSON schemas for validation
├── code-style/                  # Style guides by framework/language
│   ├── dotnet/
│   ├── angular/
│   └── typescript/
│
├── recipes/                     # Reusable patterns and solutions
│
├── tools/                       # KB transformation tools
│   ├── adapters/
│   └── templates/
│
└── tests/                       # Validation and prompt tests
    └── prompt-tests/
```
