#!/usr/bin/env node
// Lists surviving and uncovered mutants from a Stryker incremental report,
// optionally limited to the given source files, with the original code and
// the replacement no test noticed.
//
// Usage (from src/frontend/buddy):
//   node ../../../.claude/skills/mutation-fix/survivors.mjs [--report reports/stryker-incremental.json] [--json] [file.ts ...]
//
// Exits 0 when nothing survived, 1 when there are findings, 2 on bad input.
import { readFileSync } from 'node:fs';

const args = process.argv.slice(2);
const take = (name, fallback) => {
  const i = args.indexOf(name);
  if (i === -1) return fallback;
  return args.splice(i, 2)[1];
};
const reportPath = take('--report', 'reports/stryker-incremental.json');
const asJson = args.includes('--json') && args.splice(args.indexOf('--json'), 1);
// Accept both "src/app/x.ts" and comma-joined lists as the batch script prints them.
const wanted = new Set(args.flatMap((a) => a.split(',')).filter(Boolean).map((a) => a.replace(/^\.\//, '')));

let report;
try {
  report = JSON.parse(readFileSync(reportPath, 'utf8'));
} catch (err) {
  console.error(`Cannot read ${reportPath}: ${err.message}`);
  process.exit(2);
}

const FINDINGS = new Set(['Survived', 'NoCoverage']);
const result = [];
for (const [path, file] of Object.entries(report.files).sort(([a], [b]) => a.localeCompare(b))) {
  if (wanted.size && !wanted.has(path)) continue;
  const lines = file.source.split('\n');
  const counts = {};
  const findings = [];
  for (const m of file.mutants) {
    counts[m.status] = (counts[m.status] ?? 0) + 1;
    if (!FINDINGS.has(m.status)) continue;
    findings.push({
      id: m.id,
      status: m.status,
      mutator: m.mutatorName,
      line: m.location.start.line,
      original: snippet(lines, m.location),
      replacement: m.replacement ?? '',
    });
  }
  findings.sort((a, b) => a.line - b.line);
  result.push({ path, counts, findings });
}

const missing = [...wanted].filter((p) => !result.some((r) => r.path === p));
const total = result.reduce((sum, r) => sum + r.findings.length, 0);

if (asJson) {
  console.log(JSON.stringify({ total, missing, files: result }, null, 2));
} else {
  for (const { path, counts, findings } of result) {
    const detected = (counts.Killed ?? 0) + (counts.Timeout ?? 0);
    const valid = detected + (counts.Survived ?? 0) + (counts.NoCoverage ?? 0);
    const score = valid ? `${((detected * 100) / valid).toFixed(1)}%` : 'n/a';
    console.log(`\n## ${path}  score ${score}  (${findings.length} to look at)`);
    for (const f of findings) {
      const tag = f.status === 'NoCoverage' ? ' [no coverage]' : '';
      console.log(`  L${f.line} ${f.mutator}${tag} #${f.id}\n    - ${f.original}\n    + ${f.replacement.replace(/\s+/g, ' ')}`);
    }
  }
  for (const p of missing) console.log(`\n## ${p}  not in report (not mutated yet, or no mutants)`);
  console.log(`\nTotal surviving/uncovered: ${total}`);
}
process.exit(total ? 1 : 0);

function snippet(lines, { start, end }) {
  const text =
    start.line === end.line
      ? lines[start.line - 1].slice(start.column - 1, end.column - 1)
      : [lines[start.line - 1].slice(start.column - 1), ...lines.slice(start.line, end.line - 1), lines[end.line - 1].slice(0, end.column - 1)].join('\n');
  const flat = text.replace(/\s+/g, ' ').trim();
  return flat.length > 160 ? `${flat.slice(0, 157)}...` : flat;
}
