#!/usr/bin/env node

/**
 * Claude Adapter Generator
 *
 * Transforms Knowledge Base content into Claude agent configuration format.
 * Reads KB markdown files, extracts YAML frontmatter, and generates
 * a consolidated JSON config suitable for Claude agents.
 */

const fs = require('fs');
const path = require('path');
const yaml = require('js-yaml'); // npm install js-yaml

const KB_ROOT = path.join(__dirname, '../../..');
const OUTPUT_FILE = path.join(__dirname, 'claude-kb.json');

function extractFrontmatter(content) {
  const match = content.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);
  if (!match) return { frontmatter: {}, body: content };

  try {
    const frontmatter = yaml.load(match[1]);
    const body = match[2];
    return { frontmatter, body };
  } catch (err) {
    console.error('Failed to parse YAML frontmatter:', err);
    return { frontmatter: {}, body: content };
  }
}

function processDirectory(dir, baseDir = KB_ROOT) {
  const entries = [];
  const items = fs.readdirSync(dir, { withFileTypes: true });

  for (const item of items) {
    const fullPath = path.join(dir, item.name);
    const relativePath = path.relative(baseDir, fullPath);

    if (item.isDirectory()) {
      entries.push(...processDirectory(fullPath, baseDir));
    } else if (item.isFile() && item.name.endsWith('.md')) {
      const content = fs.readFileSync(fullPath, 'utf-8');
      const { frontmatter, body } = extractFrontmatter(content);

      entries.push({
        path: relativePath,
        frontmatter,
        content: body,
        estimatedTokens: frontmatter.estimatedTokens || Math.ceil(body.length / 4)
      });
    }
  }

  return entries;
}

function generateClaudeConfig() {
  const codeStyleEntries = processDirectory(path.join(KB_ROOT, 'code-style'));
  const recipeEntries = processDirectory(path.join(KB_ROOT, 'recipes'));

  const config = {
    version: '1.0.0',
    generated: new Date().toISOString(),
    codeStyles: codeStyleEntries,
    recipes: recipeEntries,
    totalEstimatedTokens: [...codeStyleEntries, ...recipeEntries]
      .reduce((sum, e) => sum + e.estimatedTokens, 0)
  };

  fs.writeFileSync(OUTPUT_FILE, JSON.stringify(config, null, 2));
  console.log(`✓ Generated Claude KB config: ${OUTPUT_FILE}`);
  console.log(`  Total entries: ${config.codeStyles.length + config.recipes.length}`);
  console.log(`  Estimated tokens: ${config.totalEstimatedTokens}`);
}

if (require.main === module) {
  generateClaudeConfig();
}

module.exports = { generateClaudeConfig };
