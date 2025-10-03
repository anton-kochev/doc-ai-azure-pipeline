#!/usr/bin/env node

/**
 * Cursor Adapter Generator
 *
 * Transforms Knowledge Base content into Cursor mode configuration format.
 * Generates .cursorrules or mode-specific configs for Cursor IDE.
 */

const fs = require('fs');
const path = require('path');
const yaml = require('js-yaml'); // npm install js-yaml

const KB_ROOT = path.join(__dirname, '../../..');
const OUTPUT_FILE = path.join(__dirname, 'cursor-kb.md');

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

function processDirectory(dir) {
  const entries = [];
  const items = fs.readdirSync(dir, { withFileTypes: true });

  for (const item of items) {
    const fullPath = path.join(dir, item.name);

    if (item.isDirectory()) {
      entries.push(...processDirectory(fullPath));
    } else if (item.isFile() && item.name.endsWith('.md')) {
      const content = fs.readFileSync(fullPath, 'utf-8');
      const { frontmatter, body } = extractFrontmatter(content);

      entries.push({ frontmatter, body });
    }
  }

  return entries;
}

function generateCursorConfig() {
  const codeStyles = processDirectory(path.join(KB_ROOT, 'code-style'));
  const recipes = processDirectory(path.join(KB_ROOT, 'recipes'));

  let output = '# Cursor AI Rules - Knowledge Base\n\n';
  output += '## Code Style Rules\n\n';

  for (const entry of codeStyles) {
    output += `### ${entry.frontmatter.title || 'Untitled'}\n\n`;
    output += entry.body + '\n\n';
  }

  output += '## Recipes\n\n';

  for (const entry of recipes) {
    output += `### ${entry.frontmatter.title || 'Untitled'}\n\n`;
    output += entry.body + '\n\n';
  }

  fs.writeFileSync(OUTPUT_FILE, output);
  console.log(`✓ Generated Cursor KB config: ${OUTPUT_FILE}`);
  console.log(`  Code styles: ${codeStyles.length}`);
  console.log(`  Recipes: ${recipes.length}`);
}

if (require.main === module) {
  generateCursorConfig();
}

module.exports = { generateCursorConfig };
