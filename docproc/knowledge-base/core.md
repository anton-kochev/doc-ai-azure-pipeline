# Knowledge Base Core Index

**Version**: 1.1.0
**Last Updated**: 2025-10-06

## Purpose

Central registry and index for the Universal Knowledge Base. This document tracks KB structure, versioning rules, and release history.

## Versioning

**Semantic Versioning**: `MAJOR.MINOR.PATCH`
- **MAJOR**: Breaking changes to schemas or structure
- **MINOR**: New recipes, style guides, or non-breaking additions
- **PATCH**: Fixes, clarifications, or typo corrections

## KB Sections

### Code Style Guides

#### .NET/C#
- [C# Core](code-style/dotnet/csharp-core.md) - Modern C# language features and conventions
- [.NET Core](code-style/dotnet/dotnet-core.md) - .NET runtime, DI, configuration, and patterns
- [ASP.NET Core](code-style/dotnet/aspnet-core.md) - Web API controllers, minimal APIs, and middleware

#### Angular/TypeScript
- [Angular](code-style/angular/angular-core.md)
  - [Components](code-style/angular/components.md)
- [TypeScript](code-style/typescript/typescript-core.md)

### Project Guidelines
- [Project Structure](code-style/project/) *(to be added)*

### Recipes
- [Table with Filters](recipes/table-with-filters.md)

### Tools & Adapters
- [Claude Adapter](tools/adapters/claude/)
- [Cursor Adapter](tools/adapters/cursor/)

## Release Notes

### v1.1.0 (2025-10-06)
- Added comprehensive C#/.NET code style guides:
  - C# Core: Modern C# 12+ language features, nullable reference types, pattern matching
  - .NET Core: Dependency injection, configuration, logging, EF Core, HTTP client patterns
  - ASP.NET Core: Controllers, minimal APIs, authentication, versioning, error handling
- Updated core index with .NET/C# section

### v1.0.0 (2025-10-03)
- Initial KB structure
- Added scaffolding for all core sections
- Created schemas for recipes and style guides
- Set up CI/CD validation pipeline
