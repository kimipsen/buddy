#!/usr/bin/env node
// Prints a markdown table of a k6 --summary-export: p95/p99 per journey and request kind, the error
// rate, and the change against a previous run's summary when one is given. The nightly workflow
// appends it to the job summary; it is a trend report, not a gate (load-testing-k6.md, "CI").
//
//   node scripts/summarize.mjs <summary.json> [<previous summary.json>]

import { existsSync, readFileSync } from 'node:fs';

const [current, previous] = process.argv.slice(2);

if (!current) {
  console.error('usage: summarize.mjs <summary.json> [<previous summary.json>]');
  process.exit(2);
}

const load = (path) =>
  path && existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')).metrics : null;
const now = load(current);
const before = load(previous);

// --summary-export writes trend stats at the top level of each metric; the newer machine-readable
// format nests them under "values". Accept both.
const stat = (metrics, name, key) => {
  const metric = metrics?.[name];
  const value = metric?.[key] ?? metric?.values?.[key];
  return typeof value === 'number' ? value : null;
};

// Only the metrics with thresholds are exported per tag. The other journeys' group durations
// include their think time, so the whole-screen number is the dashboard's alone.
const rows = [
  ['Dashboard (guardian-morning, whole screen)', 'group_duration{group:::guardian-morning}'],
  ['Reads', 'http_req_duration{kind:read}'],
  ['Writes', 'http_req_duration{kind:write}'],
  ['iCal feeds', 'http_req_duration{kind:ical}'],
];

const ms = (value) => (value === null ? '–' : `${Math.round(value)} ms`);
const delta = (a, b) => {
  if (a === null || b === null || b === 0) return '';
  const change = ((a - b) / b) * 100;
  return ` (${change >= 0 ? '+' : ''}${change.toFixed(0)} %)`;
};

const lines = ['| | p95 | p99 |', '|---|---|---|'];

for (const [label, name] of rows) {
  const p95 = stat(now, name, 'p(95)');
  if (p95 === null) continue;
  const p99 = stat(now, name, 'p(99)');
  lines.push(
    `| ${label} | ${ms(p95)}${delta(p95, stat(before, name, 'p(95)'))} | ${ms(p99)}${delta(p99, stat(before, name, 'p(99)'))} |`,
  );
}

const failed = stat(now, 'http_req_failed', 'value') ?? stat(now, 'http_req_failed', 'rate');
const requests = stat(now, 'http_reqs', 'count');
lines.push('');
lines.push(
  `${requests ?? '?'} requests, ${failed === null ? '?' : (failed * 100).toFixed(2)} % failed` +
    (before ? '. Changes are against the previous successful nightly run.' : '.'),
);

console.log(lines.join('\n'));
