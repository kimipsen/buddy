#!/usr/bin/env node
// Lints Buddy pictogram SVGs against the style guide in ../SKILL.md.
// Usage: node check-pictogram.mjs <file.svg>...   Exit code 1 when any file has an error.
import { readFileSync, statSync } from 'node:fs';
import { basename } from 'node:path';

const PALETTE = new Set([
  '#1F2937', '#F8FAFC', '#FFFFFF', '#F2C094', '#7C4A2D', '#EF4444', '#F59E0B', '#FACC15',
  '#22C55E', '#3B82F6', '#93C5FD', '#A855F7', '#F472B6', '#A16207', '#9CA3AF', '#E5E7EB',
]);
const ELEMENTS = new Set(['svg', 'title', 'g', 'rect', 'circle', 'ellipse', 'line', 'polyline', 'polygon', 'path']);
const STROKE_WIDTHS = new Set(['3', '4', '6']);
const TILE = /<rect x="0" y="0" width="96" height="96" rx="20" fill="#F8FAFC"\s*\/>/;

const files = process.argv.slice(2);
if (files.length === 0) {
  console.error('Usage: node check-pictogram.mjs <file.svg>...');
  process.exit(2);
}

let failed = false;
for (const file of files) {
  const errors = [];
  const svg = readFileSync(file, 'utf8');

  if (!/^[a-z0-9]+(-[a-z0-9]+)*\.svg$/.test(basename(file))) errors.push('file name is not kebab-case .svg');
  if (statSync(file).size > 4096) errors.push(`file is ${statSync(file).size} bytes, limit 4096`);
  if (!svg.endsWith('\n')) errors.push('file does not end with a newline');

  const root = svg.match(/<svg\b[^>]*>/)?.[0] ?? '';
  for (const attr of ['xmlns="http://www.w3.org/2000/svg"', 'viewBox="0 0 96 96"', 'width="96"', 'height="96"', 'role="img"', 'aria-labelledby="title"']) {
    if (!root.includes(attr)) errors.push(`<svg> is missing ${attr}`);
  }
  if (!/<title id="title">[^<]{2,}<\/title>/.test(svg)) errors.push('missing <title id="title"> with a label');
  if (!TILE.test(svg)) errors.push('missing the standard tile <rect x="0" y="0" width="96" height="96" rx="20" fill="#F8FAFC"/>');

  for (const [, name] of svg.matchAll(/<([a-zA-Z][\w:-]*)/g)) {
    if (!ELEMENTS.has(name)) errors.push(`element <${name}> is not allowed`);
  }
  for (const [, attr] of svg.matchAll(/\s([a-zA-Z:-]+)=/g)) {
    if (attr === 'style' || attr === 'class' || attr.startsWith('on') || attr.includes('href') || attr === 'opacity' || attr.endsWith('-opacity') || attr === 'stroke-dasharray' || attr === 'filter') {
      errors.push(`attribute ${attr} is not allowed`);
    }
  }
  for (const [, attr, value] of svg.matchAll(/\s(fill|stroke)="([^"]*)"/g)) {
    if (value === 'none') continue;
    if (!PALETTE.has(value.toUpperCase())) errors.push(`${attr}="${value}" is not in the palette`);
    else if (value !== value.toUpperCase()) errors.push(`${attr}="${value}" should be upper-case hex`);
  }
  for (const [, value] of svg.matchAll(/\sstroke-width="([^"]*)"/g)) {
    if (!STROKE_WIDTHS.has(value)) errors.push(`stroke-width="${value}" is not 3, 4 or 6`);
  }
  if (/url\(|data:|https?:\/\/(?!www\.w3\.org\/2000\/svg)/.test(svg)) errors.push('external reference or url() found');
  for (const [number] of svg.matchAll(/-?\d+\.\d+/g)) {
    if (!/\.5$|\.0$/.test(number)) errors.push(`coordinate ${number} is not an integer or .5`);
  }

  if (errors.length) {
    failed = true;
    console.log(`✗ ${file}`);
    for (const error of [...new Set(errors)]) console.log(`  - ${error}`);
  } else {
    console.log(`✓ ${file}`);
  }
}
process.exit(failed ? 1 : 0);
