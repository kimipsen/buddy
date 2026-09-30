#!/usr/bin/env node
// Turns a Stryker mutation report (mutation-testing-report-schema JSON, e.g.
// reports/stryker-incremental.json) into a Markdown issue body listing every
// surviving and uncovered mutant, grouped by file, as a checklist.
//
// Usage:
//   node scripts/mutation-issue.mjs [report.json] [--out full.md] [--max-chars N]
//
// Writes the (possibly truncated) body to stdout and, with --out, the full,
// untruncated body to a file. Exits 0 either way; the body says when there's
// nothing to fix. When GITHUB_REPOSITORY and GITHUB_SHA are set, locations
// link to the file at that commit; REPORT_PATH_PREFIX (default
// src/frontend/buddy/) maps report paths to repo paths.
import { readFileSync, writeFileSync } from 'node:fs';

const args = process.argv.slice(2);
const flag = (name, fallback) => {
  const i = args.indexOf(name);
  if (i === -1) return fallback;
  const [, value] = args.splice(i, 2);
  return value;
};
const outFile = flag('--out');
// GitHub rejects issue bodies over 65536 characters.
const maxChars = Number(flag('--max-chars', '60000'));
const reportPath = args[0] ?? 'reports/stryker-incremental.json';

const report = JSON.parse(readFileSync(reportPath, 'utf8'));
const { GITHUB_REPOSITORY: repo, GITHUB_SHA: sha, GITHUB_SERVER_URL: server = 'https://github.com' } = process.env;
const prefix = process.env.REPORT_PATH_PREFIX ?? 'src/frontend/buddy/';

const FINDINGS = new Set(['Survived', 'NoCoverage']);
const counts = {};
const files = [];

for (const [path, file] of Object.entries(report.files).sort(([a], [b]) => a.localeCompare(b))) {
  const lines = file.source.split('\n');
  const findings = [];
  for (const mutant of file.mutants) {
    counts[mutant.status] = (counts[mutant.status] ?? 0) + 1;
    if (FINDINGS.has(mutant.status)) findings.push({ ...mutant, original: snippet(lines, mutant.location) });
  }
  if (findings.length === 0) continue;
  findings.sort((a, b) => a.location.start.line - b.location.start.line || a.location.start.column - b.location.start.column);
  files.push({ path, findings });
}

const n = (status) => counts[status] ?? 0;
const detected = n('Killed') + n('Timeout');
const valid = detected + n('Survived') + n('NoCoverage');
const score = valid > 0 ? `${((detected * 100) / valid).toFixed(2)}%` : 'n/a';
const total = files.reduce((sum, f) => sum + f.findings.length, 0);

const header = [
  `Mutation score: **${score}** · ${n('Survived')} survived · ${n('NoCoverage')} without coverage · ` +
    `${n('Killed')} killed · ${n('Timeout')} timed out · ${n('CompileError')} compile errors`,
  '',
  sha ? `Generated from \`${sha.slice(0, 7)}\`${process.env.GITHUB_RUN_ID ? ` by [this run](${server}/${repo}/actions/runs/${process.env.GITHUB_RUN_ID})` : ''}. ` +
    'This issue is rewritten by every nightly run, so ticked boxes are not kept; a fixed mutant simply disappears.' : '',
  '',
  'Each entry is `line: Mutator` followed by the original code and the replacement that no test noticed. ' +
    'Fix a finding by adding or tightening a test so it fails for the replacement; ' +
    '[equivalent mutants](https://stryker-mutator.io/docs/mutation-testing-elements/equivalent-mutants/) can be ignored with a `// Stryker disable next-line <Mutator>: <reason>` comment.',
  '',
].join('\n');

const sections = files.map(({ path, findings }) => {
  const items = findings.map((m) => {
    const line = m.location.start.line;
    const where = repo && sha ? `[L${line}](${server}/${repo}/blob/${sha}/${prefix}${path}#L${line})` : `L${line}`;
    const tag = m.status === 'NoCoverage' ? ' *(no coverage)*' : '';
    return `- [ ] ${where}: \`${m.mutatorName}\`${tag}: ${code(m.original)} → ${code(m.replacement ?? '')}`;
  });
  return `### \`${path}\` (${findings.length})\n\n${items.join('\n')}\n`;
});

const empty = total === 0 ? 'No surviving or uncovered mutants. 🎉\n' : '';
const full = `${header}\n${empty}${sections.join('\n')}`;
if (outFile) writeFileSync(outFile, full);

let body = `${header}\n${empty}`;
let shown = 0;
for (const section of sections) {
  // Leave room for the truncation note.
  if (body.length + section.length > maxChars - 300) break;
  body += `${section}\n`;
  shown++;
}
if (shown < sections.length) {
  body += `\n> [!NOTE]\n> ${sections.length - shown} more file(s) did not fit in an issue body. ` +
    'The full list is in the `mutation-findings.md` file of the run\'s `frontend-mutation-report` artifact.\n';
}
process.stdout.write(body);

// Stryker locations are 1-based lines and columns, with an exclusive end column.
function snippet(lines, { start, end }) {
  const picked = lines.slice(start.line - 1, end.line);
  if (picked.length === 0) return '';
  picked[picked.length - 1] = picked[picked.length - 1].slice(0, end.column - 1);
  picked[0] = picked[0].slice(start.column - 1);
  return picked.join('\n');
}

// Inline code that survives backticks and newlines, truncated to stay readable.
function code(text) {
  let s = text.replace(/\s+/g, ' ').trim();
  if (s.length > 120) s = `${s.slice(0, 117)}...`;
  if (s === '') return '*(empty)*';
  const fence = '`'.repeat(Math.max(0, ...(s.match(/`+/g) ?? []).map((r) => r.length)) + 1);
  const pad = s.startsWith('`') || s.endsWith('`') ? ' ' : '';
  return `${fence}${pad}${s}${pad}${fence}`;
}
