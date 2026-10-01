#!/usr/bin/env node
// Writes a copy of buddy.IntegrationTests/stryker-config.json whose "mutate"
// list is limited to the given files/globs, with the Json reporter added (the
// checked-in config has only Progress/Html/cleartext) and an absolute solution
// path so the copy can live outside the repo (e.g. the scratchpad).
//
// Stryker.NET (verified on 4.16) ignores --mutate on the CLI when the config file sets
// "mutate", so scoping goes through a separate config passed with -f instead
// of editing the checked-in file.
//
// Usage (from src/backend/buddy.IntegrationTests):
//   node ../../../.claude/skills/mutation-fix-backend/scoped-config.mjs --out <file.json> [--concurrency N] <glob-or-file> ...
// Paths are relative to src/backend/buddy (the project under test), e.g.
//   Features/Guardians/CreateChild/**/*.cs  or  Features/Guardians/CreateChild/CreateChild.Handler.cs
// A leading "buddy/" or "src/backend/buddy/" is stripped.
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const args = process.argv.slice(2);
const take = (name, fallback) => {
  const i = args.indexOf(name);
  if (i === -1) return fallback;
  return args.splice(i, 2)[1];
};
const out = take('--out', undefined);
const concurrency = take('--concurrency', undefined);
const globs = args
  .flatMap((a) => a.split(','))
  .filter(Boolean)
  .map((a) => a.replaceAll('\\', '/').replace(/^\.\//, '').replace(/^(src\/backend\/)?buddy\//, ''));

if (!out || !globs.length) {
  console.error('Usage: scoped-config.mjs --out <file.json> [--concurrency N] <glob-or-file> ...');
  process.exit(2);
}

const base = JSON.parse(readFileSync('stryker-config.json', 'utf8'));
const cfg = base['stryker-config'];
const excludes = (cfg.mutate ?? []).filter((m) => m.startsWith('!'));
cfg.mutate = [...globs, ...excludes];
cfg.solution = resolve(cfg.solution ?? '../backend.slnx');
const reporters = new Set((cfg.reporters ?? []).map((r) => r.toLowerCase()));
cfg.reporters = [...(cfg.reporters ?? []), ...(reporters.has('json') ? [] : ['Json'])];
if (concurrency) cfg.concurrency = Number(concurrency);

writeFileSync(out, `${JSON.stringify(base, null, 2)}\n`);
console.log(`Wrote ${out}: mutate ${JSON.stringify(cfg.mutate)}, concurrency ${cfg.concurrency}`);
