# Contributing to the Knowledge Base

## Overview

Thank you for contributing to the Universal Knowledge Base! This document provides guidelines for adding, modifying, and reviewing KB entries.

## Contribution Process

1. **Create a branch** from `main` or `develop`
2. **Add/modify KB entries** following the guidelines below
3. **Validate your changes** using the provided tools
4. **Submit a pull request** with clear description
5. **Address review feedback** from KB owners

## Guidelines

### Adding a Recipe

1. Create a new `.md` file in `knowledge-base/recipes/`
2. Include YAML frontmatter matching `recipe.schema.json`
3. Structure the recipe:
   - **Problem**: What issue does this solve?
   - **Solution**: High-level approach
   - **Implementation**: Code examples with annotations
   - **Notes**: Caveats, alternatives, or extensions

Example:
```markdown
---
id: my-recipe
title: My Recipe
category: ui
tags: ["angular", "components"]
version: 1.0.0
frameworks: ["angular"]
difficulty: intermediate
estimatedTokens: 500
---

# My Recipe

## Problem
...
```

### Adding a Style Guide

1. Create a new `.md` file in `knowledge-base/code-style/{framework}/`
2. Include YAML frontmatter matching `style.schema.json`
3. Use clear, actionable rules with examples
4. Specify priority level (must/should/may per RFC 2119)

### Modifying Existing Entries

- Increment version number according to semver
- Document breaking changes in the entry
- Update `core.md` release notes

## Validation Checklist

Before submitting your PR:

- [ ] YAML frontmatter validates against schemas
- [ ] Markdown formatting is correct
- [ ] Code examples are tested and functional
- [ ] Links are valid and not broken
- [ ] Estimated token count is accurate (use `wc -w file.md | awk '{print $1 * 1.3}'`)
- [ ] Generated configs (Claude/Cursor) build successfully
- [ ] Entry is referenced in `core.md` index

## Running Local Validation

```bash
# Validate schemas
cd knowledge-base/tools/adapters/claude
npm install js-yaml ajv
node -e "
  const Ajv = require('ajv');
  const fs = require('fs');
  const ajv = new Ajv();
  const schema = JSON.parse(fs.readFileSync('../../schemas/recipe.schema.json'));
  ajv.compile(schema);
  console.log('Valid');
"

# Generate configs
node generator.js

cd ../cursor
node generator.js
```

## Review Process

KB entries are reviewed by section owners (see `OWNERS` file).

**Review criteria:**
- Accuracy and correctness
- Clarity and conciseness
- Consistency with existing KB style
- Token efficiency (avoid redundancy)
- Framework/tool version compatibility

**Approval requirements:**
- At least 1 owner approval for minor changes
- At least 2 owner approvals for breaking changes

## Style Guidelines

- Use imperative mood for rules ("Use X" not "You should use X")
- Keep examples minimal but complete
- Avoid opinionated language unless backed by community standards
- Reference official docs where applicable
- Optimize for token efficiency (AI context window)

## Token Efficiency Tips

- Remove redundant explanations
- Use code comments instead of prose where possible
- Link to external docs for deep dives
- Use schema frontmatter to avoid repeating metadata

## Questions?

Open an issue with the `question` label or contact KB maintainers.
