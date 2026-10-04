#!/usr/bin/env node
// Lists surviving and uncovered mutants from a Stryker.NET JSON report
// (mutation-report.json, written by the Json reporter), optionally limited to
// the given source files, with the original code, the replacement no test
// noticed, and the mutator name to use in a "// Stryker disable once" comment.
//
// Usage (from src/backend/buddy.IntegrationTests):
//   node ../../../.agents/skills/mutation-fix-backend/survivors.mjs [--report <path>] [--json] [file.cs ...]
//
// Without --report it reads StrykerOutput/mutation-fix/reports/mutation-report.json
// (where the skill's runs write to), or else the newest
// StrykerOutput/*/reports/mutation-report.json.
// Files may be given as "Features/X/Y.cs", "buddy/Features/X/Y.cs", absolute
// paths, or comma-joined lists; they match report entries by path suffix.
//
// Exits 0 when nothing survived, 1 when there are findings, 2 on bad input.
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

const args = process.argv.slice(2);
const take = (name, fallback) => {
  const i = args.indexOf(name);
  if (i === -1) return fallback;
  return args.splice(i, 2)[1];
};
const reportPath = take('--report', undefined) ?? findReport();
const asJson = args.includes('--json') && args.splice(args.indexOf('--json'), 1);
const norm = (p) => p.replaceAll('\\', '/').replace(/^\.\//, '');
const wanted = args.flatMap((a) => a.split(',')).filter(Boolean).map(norm);

let report;
try {
  if (!reportPath) throw new Error('no StrykerOutput/*/reports/mutation-report.json found; pass --report');
  report = JSON.parse(readFileSync(reportPath, 'utf8'));
} catch (err) {
  console.error(`Cannot read ${reportPath ?? 'a mutation report'}: ${err.message}`);
  console.error('Was the run started with the Json reporter (-r Json, or "Json" in the config\'s reporters)?');
  process.exit(2);
}

// Stryker.NET keys files by absolute path; show them relative to the project root.
const root = report.projectRoot ? norm(report.projectRoot).replace(/\/?$/, '/') : '';
const display = (p) => (root && norm(p).startsWith(root) ? norm(p).slice(root.length) : norm(p));
const matches = (key, w) => {
  const k = norm(key);
  return k === w || k.endsWith(`/${w}`) || w.endsWith(`/${display(key)}`);
};

const FINDINGS = new Set(['Survived', 'NoCoverage']);
// Stryker.NET 5 lists every source file of the project, also the ones outside the run's "mutate"
// scope: those have an empty mutants array, or only mutants with statusReason
// "Removed by mutate filter". Treat such files as not mutated in this run.
const inScope = (file) => (file.mutants ?? []).some((m) => m.statusReason !== 'Removed by mutate filter');
const mutated = Object.entries(report.files ?? {}).filter(([, file]) => inScope(file));
const result = [];
for (const [key, file] of mutated.toSorted(([a], [b]) => a.localeCompare(b))) {
  if (wanted.length && !wanted.some((w) => matches(key, w))) continue;
  const lines = (file.source ?? '').split('\n');
  const counts = {};
  const findings = [];
  for (const m of file.mutants ?? []) {
    counts[m.status] = (counts[m.status] ?? 0) + 1;
    if (!FINDINGS.has(m.status)) continue;
    findings.push({
      id: m.id,
      status: m.status,
      mutator: m.mutatorName,
      reason: m.statusReason,
      disableName: disableName(m.mutatorName),
      line: m.location.start.line,
      original: snippet(lines, m.location),
      replacement: m.replacement ?? '',
    });
  }
  findings.sort((a, b) => a.line - b.line);
  result.push({ path: display(key), counts, findings });
}

const missing = wanted.filter((w) => !mutated.some(([k]) => matches(k, w)));
const total = result.reduce((sum, r) => sum + r.findings.length, 0);

if (asJson) {
  console.log(JSON.stringify({ report: reportPath, total, missing, files: result }, null, 2));
} else {
  console.log(`Report: ${reportPath}`);
  for (const { path, counts, findings } of result) {
    const detected = (counts.Killed ?? 0) + (counts.Timeout ?? 0);
    const valid = detected + (counts.Survived ?? 0) + (counts.NoCoverage ?? 0);
    const score = valid ? `${((detected * 100) / valid).toFixed(1)}%` : 'n/a';
    const extra = ['Ignored', 'CompileError'].filter((s) => counts[s]).map((s) => `${counts[s]} ${s}`).join(', ');
    const extraSuffix = extra ? `; ${extra}` : '';
    console.log(`\n## ${path}  score ${score}  (${findings.length} to look at${extraSuffix})`);
    for (const f of findings) {
      const tag = f.status === 'NoCoverage' ? ' [no coverage]' : '';
      console.log(
        `  L${f.line} ${f.mutator}${tag} #${f.id}  (disable as: ${f.disableName})\n    - ${f.original}\n    + ${f.replacement.replace(/\s+/g, ' ')}`,
      );
    }
  }
  for (const p of missing) console.log(`\n## ${p}  not in report (not mutated in this run, or no mutants)`);
  console.log(`\nTotal surviving/uncovered: ${total}`);
}
process.exit(total ? 1 : 0);

function findReport() {
  const out = 'StrykerOutput';
  const fixed = join(out, 'mutation-fix', 'reports', 'mutation-report.json');
  if (existsSync(fixed)) return fixed;
  if (!existsSync(out)) return undefined;
  const candidates = readdirSync(out)
    .map((d) => join(out, d, 'reports', 'mutation-report.json'))
    .filter((p) => existsSync(p))
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs);
  return candidates[0];
}

// Maps the report's display name (e.g. "Equality mutation", "Block removal mutation")
// to the Mutator name Stryker.NET's "// Stryker disable" comments expect.
function disableName(name = '') {
  const n = name.toLowerCase();
  const table = [
    ['block removal', 'Block'],
    ['statement', 'Statement'],
    ['string method', 'StringMethod'],
    ['string', 'String'],
    ['equality', 'Equality'],
    ['logical', 'Logical'],
    ['bitwise', 'Bitwise'],
    ['arithmetic', 'Arithmetic'],
    ['assignment', 'Assignment'],
    ['boolean', 'Boolean'],
    ['negate', 'Boolean'],
    ['conditional', 'Conditional'],
    ['linq', 'Linq'],
    ['math', 'Math'],
    ['null coalescing', 'NullCoalescing'],
    ['collection expression', 'CollectionExpression'],
    ['initializer', 'Initializer'],
    ['checked', 'Checked'],
    ['regex', 'Regex'],
    ['increment', 'Update'],
    ['decrement', 'Update'],
    ['unary', 'Unary'],
  ];
  return table.find(([k]) => n.includes(k))?.[1] ?? 'all';
}

function snippet(lines, { start, end }) {
  const text =
    start.line === end.line
      ? (lines[start.line - 1] ?? '').slice(start.column - 1, end.column - 1)
      : [
          (lines[start.line - 1] ?? '').slice(start.column - 1),
          ...lines.slice(start.line, end.line - 1),
          (lines[end.line - 1] ?? '').slice(0, end.column - 1),
        ].join('\n');
  const flat = text.replace(/\s+/g, ' ').trim();
  return flat.length > 160 ? `${flat.slice(0, 157)}...` : flat;
}
