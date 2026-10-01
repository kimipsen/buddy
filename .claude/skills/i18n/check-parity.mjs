#!/usr/bin/env node
// Compares the en and da translation dictionaries of the Buddy frontend and reports:
//   - keys missing in either language (exit code 1)
//   - {placeholder} mismatches between en and da for the same key (exit code 1)
//   - keys whose da value is identical to en (warning only: may be an untranslated copy)
//   - with --unused: keys never referenced from src/app (warning only)
//
// Usage (works from any cwd; paths are resolved relative to this file):
//   node .claude/skills/i18n/check-parity.mjs [--unused] [--no-identical] [--json]
//
// No dependencies: each translations/<lang>/<area>.ts is a plain `export const x = { ... }` object
// literal, so it's type-stripped (node:module stripTypeScriptTypes, when available) and evaluated.

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import * as nodeModule from 'node:module';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = new Set(process.argv.slice(2));
const checkUnused = args.has('--unused');
const checkIdentical = !args.has('--no-identical');
const asJson = args.has('--json');

const APP_REL = 'src/frontend/buddy/src/app';
const I18N_REL = `${APP_REL}/core/i18n/translations`;

function findRepoRoot() {
  const candidates = [
    resolve(dirname(fileURLToPath(import.meta.url)), '../../..'),
    process.cwd()
  ];
  for (const start of candidates) {
    let dir = start;
    for (;;) {
      if (existsSync(join(dir, I18N_REL))) return dir;
      const parent = dirname(dir);
      if (parent === dir) break;
      dir = parent;
    }
  }
  // Running from inside src/frontend/buddy (or below) with no repo-relative match.
  console.error(`Could not locate ${I18N_REL} from the script location or the current directory.`);
  process.exit(2);
}

const repoRoot = findRepoRoot();
const appDir = join(repoRoot, APP_REL);
const translationsDir = join(repoRoot, I18N_REL);

function stripTypes(source) {
  if (typeof nodeModule.stripTypeScriptTypes === 'function') {
    return nodeModule.stripTypeScriptTypes(source);
  }
  return source.replace(/\bas\s+const\b/g, '').replace(/\bsatisfies\s+[\w.<>[\], ]+/g, '');
}

function evaluateAreaFile(file) {
  const source = stripTypes(readFileSync(file, 'utf8'));
  const match = /export\s+const\s+(\w+)\s*(?::[^=]+)?=/.exec(source);
  if (!match) throw new Error(`${relative(repoRoot, file)}: no "export const x = {...}" found`);
  const body = source.slice(0, match.index) + 'return ' + source.slice(match.index + match[0].length);
  return { name: match[1], value: new Function(body)() };
}

// Builds a language's dictionary from its index.ts: `import { a } from './a'` plus
// `export const en = { a, b: c, ... }` (shorthand or `key: identifier`).
function loadLanguage(lang) {
  const langDir = join(translationsDir, lang);
  const index = readFileSync(join(langDir, 'index.ts'), 'utf8');
  const imports = new Map();
  for (const m of index.matchAll(/import\s*\{([^}]+)\}\s*from\s*['"](\.\/[^'"]+)['"]/g)) {
    for (const spec of m[1].split(',').map((s) => s.trim()).filter(Boolean)) {
      const [imported, local = imported] = spec.split(/\s+as\s+/).map((s) => s.trim());
      imports.set(local, { file: join(langDir, `${m[2].slice(2)}.ts`), imported });
    }
  }
  const objMatch = /export\s+const\s+\w+\s*(?::[^=]+)?=\s*\{([\s\S]*?)\}\s*;?/.exec(index);
  if (!objMatch) throw new Error(`${lang}/index.ts: no exported dictionary object found`);
  const dict = {};
  for (const entry of objMatch[1].split(',').map((s) => s.trim()).filter(Boolean)) {
    const [key, ident = key] = entry.split(':').map((s) => s.trim());
    const source = imports.get(ident);
    if (!source) throw new Error(`${lang}/index.ts: "${ident}" is not imported`);
    const { name, value } = evaluateAreaFile(source.file);
    if (name !== source.imported) {
      throw new Error(`${relative(repoRoot, source.file)} exports "${name}", expected "${source.imported}"`);
    }
    dict[key] = value;
  }
  return dict;
}

function flatten(node, prefix = '', out = new Map()) {
  for (const [key, value] of Object.entries(node)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value && typeof value === 'object') flatten(value, path, out);
    else out.set(path, value);
  }
  return out;
}

const placeholders = (value) => [...String(value).matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join(',');

const en = flatten(loadLanguage('en'));
const da = flatten(loadLanguage('da'));

const missingInDa = [...en.keys()].filter((k) => !da.has(k));
const missingInEn = [...da.keys()].filter((k) => !en.has(k));
const placeholderMismatch = [...en.keys()]
  .filter((k) => da.has(k) && placeholders(en.get(k)) !== placeholders(da.get(k)))
  .map((k) => ({ key: k, en: en.get(k), da: da.get(k) }));
const identical = checkIdentical
  ? [...en.keys()].filter((k) => da.has(k) && en.get(k) === da.get(k)).map((k) => ({ key: k, value: en.get(k) }))
  : [];

// --- Unused-key scan -------------------------------------------------------------------------
// Usage patterns in this app: a full key as a string literal anywhere in non-spec .ts/.html under
// src/app ('x.y' | translate, translation.translate('x.y'), error.set('x.y'), Record<..., string>
// maps of keys). Dynamic keys are a literal prefix ending in '.' joined to a runtime value
// ('shell.menu.theme.' + mode, `x.y.${z}`): keys under such a prefix are "possibly unused".
let unused = [];
let possiblyUnused = [];
let dynamicPrefixes = [];
if (checkUnused) {
  const sources = [];
  const walk = (dir) => {
    for (const name of readdirSync(dir)) {
      const full = join(dir, name);
      if (statSync(full).isDirectory()) {
        if (full !== translationsDir) walk(full);
      } else if (/\.(ts|html)$/.test(name) && !name.endsWith('.spec.ts')) {
        sources.push(readFileSync(full, 'utf8'));
      }
    }
  };
  walk(appDir);
  const text = sources.join('\n');

  const literals = new Set();
  for (const m of text.matchAll(/(['"`])([A-Za-z][\w-]*(?:\.[\w-]+)+)\1/g)) literals.add(m[2]);

  const prefixes = new Set();
  for (const m of text.matchAll(/(['"])([A-Za-z][\w-]*(?:\.[\w-]+)*\.)\1\s*\+/g)) prefixes.add(m[2]);
  for (const m of text.matchAll(/`([A-Za-z][\w-]*(?:\.[\w-]+)*\.)\$\{/g)) prefixes.add(m[1]);
  dynamicPrefixes = [...prefixes].filter((p) => [...en.keys()].some((k) => k.startsWith(p))).sort();

  for (const key of en.keys()) {
    if (literals.has(key)) continue;
    const prefix = dynamicPrefixes.find((p) => key.startsWith(p));
    if (prefix) possiblyUnused.push({ key, prefix });
    else unused.push(key);
  }
}

const failed = missingInDa.length + missingInEn.length + placeholderMismatch.length > 0;

if (asJson) {
  console.log(JSON.stringify({
    keys: { en: en.size, da: da.size },
    missingInDa, missingInEn, placeholderMismatch, identical,
    ...(checkUnused ? { unused, possiblyUnused, dynamicPrefixes } : {})
  }, null, 2));
  process.exit(failed ? 1 : 0);
}

const section = (title, items, render = (x) => x) => {
  console.log(`\n${title} (${items.length})`);
  for (const item of items) console.log(`  ${render(item)}`);
};

console.log(`Translations: ${relative(process.cwd(), translationsDir) || '.'}`);
console.log(`Keys: en=${en.size} da=${da.size}`);
section('Missing in da', missingInDa);
section('Missing in en', missingInEn);
section('Placeholder mismatch', placeholderMismatch, (x) => `${x.key}\n      en: ${x.en}\n      da: ${x.da}`);
if (checkIdentical) {
  section('Identical en/da value (warning: check it is really Danish)', identical, (x) => `${x.key} = ${JSON.stringify(x.value)}`);
}
if (checkUnused) {
  section('Unused in src/app (warning)', unused);
  section('Possibly unused: only reachable via a dynamic key (warning)', possiblyUnused, (x) => `${x.key}  [via '${x.prefix}' + …]`);
}
console.log(failed ? '\nFAIL: en/da are out of parity.' : '\nOK: en/da keys are in parity.');
process.exit(failed ? 1 : 0);
