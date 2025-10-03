# Universal Knowledge Base

## Overview

This is a modular, token-efficient Knowledge Base designed for both human developers and AI agents. It provides a canonical source of truth for code style guides, architectural patterns, recipes, and best practices.

## Philosophy

- **Single Source of Truth**: One KB, multiple consumers (humans, Claude, Cursor, etc.)
- **Token-Efficient**: Structured for AI context windows with minimal redundancy
- **Modular**: Organized by domain with clear separation of concerns
- **Machine-Readable**: JSON schemas + YAML frontmatter for automation
- **Version-Controlled**: All changes tracked, reviewed, and tested

## Usage

### For Humans
Browse the KB directly via Markdown files. Start with `core.md` for the index.

### For AI Agents
Adapters transform KB content into agent-specific formats:
- **Claude**: `tools/adapters/claude/generator.js`
- **Cursor**: `tools/adapters/cursor/generator.js`

### Contributing
See `CONTRIBUTING.md` for guidelines and review process.

## Structure

```
knowledge-base/
├── core.md              # Central index and versioning
├── schemas/             # JSON schemas for validation
├── code-style/          # Style guides by framework
├── recipes/             # Reusable patterns and solutions
├── tools/               # KB transformation tools
└── tests/               # Validation and prompt tests
```
