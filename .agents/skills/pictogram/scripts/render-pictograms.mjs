#!/usr/bin/env node
// Renders pictogram SVGs into one PNG contact sheet: each file at 96px and 48px, on the light
// and the dark app background, so a run can check recognisability and consistency by eye.
// Usage: node render-pictograms.mjs <out.png> <file.svg>...
// Uses the Playwright install in src/frontend/buddy (run `npm ci` there first in a fresh worktree).
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { basename, resolve } from 'node:path';

const [out, ...files] = process.argv.slice(2);
if (!out || files.length === 0) {
  console.error('Usage: node render-pictograms.mjs <out.png> <file.svg>...');
  process.exit(2);
}

const require = createRequire(resolve('src/frontend/buddy/package.json'));
const { chromium } = require('playwright');

const escape = (text) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;');
const cell = (file) => {
  const src = `data:image/svg+xml;base64,${readFileSync(file).toString('base64')}`;
  return `<figure><div class="row"><img src="${src}" width="96" height="96"><img src="${src}" width="48" height="48"></div><figcaption>${escape(basename(file, '.svg'))}</figcaption></figure>`;
};
const sheet = (background, color) =>
  `<section style="background:${background};color:${color}">${files.map(cell).join('')}</section>`;
const html = `<!doctype html><html><head><style>
  body { margin: 0; font: 12px system-ui, sans-serif; }
  section { display: flex; flex-wrap: wrap; gap: 16px; padding: 16px; }
  figure { margin: 0; display: flex; flex-direction: column; align-items: center; gap: 4px; }
  .row { display: flex; align-items: flex-end; gap: 8px; }
</style></head><body>${sheet('#FFFFFF', '#1F2937')}${sheet('#0F172A', '#E5E7EB')}</body></html>`;

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: Math.min(1200, 32 + files.length * 176), height: 200 } });
await page.setContent(html);
await page.screenshot({ path: out, fullPage: true });
await browser.close();
console.log(`Wrote ${out}`);
