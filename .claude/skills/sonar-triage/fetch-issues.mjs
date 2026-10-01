#!/usr/bin/env node
// Fetches open SonarCloud/SonarQube issues for a project through the web API
// (api/issues/search) and prints them one per line, sorted by file and line,
// plus a per-rule count. No dependencies (uses the built-in fetch).
//
// Usage (from the repo root):
//   node .claude/skills/sonar-triage/fetch-issues.mjs --project <key> [--branch <name> | --pr <number>] [--json]
//   node .claude/skills/sonar-triage/fetch-issues.mjs --from <saved-response.json>   (parse a saved response instead)
//
// Environment (never pass the token on the command line or paste it in chat):
//   SONAR_TOKEN        user token; optional for public projects
//   SONAR_PROJECT_KEY  default for --project
//   SONAR_HOST_URL     default https://sonarcloud.io
//
// Exits 0 when there are no open issues, 1 when there are, 2 on errors.
import { readFileSync } from 'node:fs';

const args = process.argv.slice(2);
const take = (name, fallback) => {
  const i = args.indexOf(name);
  if (i === -1) return fallback;
  return args.splice(i, 2)[1];
};
const project = take('--project', process.env.SONAR_PROJECT_KEY);
const branch = take('--branch', undefined);
const pr = take('--pr', undefined);
const from = take('--from', undefined);
const asJson = args.includes('--json');
const host = (process.env.SONAR_HOST_URL || 'https://sonarcloud.io').replace(/\/$/, '');

let issues;
try {
  issues = from ? JSON.parse(readFileSync(from, 'utf8')).issues : await fetchAll();
} catch (err) {
  console.error(`sonar-triage: ${err.message}`);
  process.exit(2);
}

const rows = issues
  .map((i) => ({
    key: i.key,
    rule: i.rule,
    path: i.component?.includes(':') ? i.component.slice(i.component.indexOf(':') + 1) : i.component,
    line: i.line ?? i.textRange?.startLine ?? null,
    severity: i.impacts?.map((x) => `${x.softwareQuality}:${x.severity}`).join(',') || i.severity || '',
    type: i.type ?? '',
    message: i.message,
  }))
  .sort((a, b) => a.path.localeCompare(b.path) || (a.line ?? 0) - (b.line ?? 0));

if (asJson) {
  console.log(JSON.stringify({ total: rows.length, issues: rows }, null, 2));
} else {
  for (const r of rows) console.log(`${r.rule}\t${r.path}:${r.line ?? '-'}\t${r.severity}\t${r.message}\t[${r.key}]`);
  const byRule = Object.entries(Object.groupBy(rows, (r) => r.rule)).sort((a, b) => b[1].length - a[1].length);
  const summary = byRule.map(([rule, rs]) => rule + '×' + rs.length).join(', ');
  console.log(`\n${rows.length} open issue(s): ${summary}`);
}
process.exit(rows.length ? 1 : 0);

async function fetchAll() {
  if (!project) throw new Error('no project key: pass --project or set SONAR_PROJECT_KEY');
  const headers = process.env.SONAR_TOKEN ? { Authorization: `Bearer ${process.env.SONAR_TOKEN}` } : {};
  // issues/search returns an empty list (not an error) for an unknown or inaccessible
  // project, so confirm the project exists first.
  const check = await fetch(`${host}/api/components/show?${new URLSearchParams({ component: project })}`, { headers });
  if (!check.ok) {
    const hint = process.env.SONAR_TOKEN ? 'check the key and the token\'s access' : 'private projects need SONAR_TOKEN';
    throw new Error(`project '${project}' not found on ${host} (HTTP ${check.status}); ${hint}`);
  }
  const all = [];
  for (let p = 1; ; p++) {
    const q = new URLSearchParams({ componentKeys: project, resolved: 'false', ps: '500', p: String(p) });
    if (branch) q.set('branch', branch);
    if (pr) q.set('pullRequest', pr);
    const res = await fetch(`${host}/api/issues/search?${q}`, { headers });
    const body = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(`HTTP ${res.status} from ${host}: ${body.errors?.map((e) => e.msg).join('; ') ?? res.statusText}`);
    all.push(...body.issues);
    const total = body.paging?.total ?? body.total ?? 0;
    // The API refuses to page past 10,000 results.
    if (all.length >= total || body.issues.length === 0 || p * 500 >= 10000) break;
  }
  return all;
}
