#!/usr/bin/env node
// Turns a Stryker mutation report (mutation-testing-report-schema JSON, e.g.
// reports/stryker-incremental.json) into Markdown issue bodies listing every
// surviving and uncovered mutant, grouped by file, as a checklist.
//
// Usage:
//   node scripts/mutation-issue.mjs [report.json] [--out full.md] [--out-dir dir]
//                                   [--max-chars N] [--stale stale.txt]
//
// Stdout gets the full combined body (every file with findings), for local use.
// --out writes the same full body to a file.
//
// --out-dir splits the findings over groups of at most 5 report files, one
// rolling GitHub issue per group. It writes `manifest.json` and one
// `<3-digit index>-<slug>.md` body per group that has findings. A group is
// derived from the file path alone (feature folder, large folders split by a
// path hash), never from the position in a sorted list, so fixing mutants
// never moves a file to another issue. Each body starts with a hidden marker,
// `<!-- mutation-group: KEY -->`, that maps the body to its issue. A body over
// --max-chars (default 60000, GitHub caps at 65536) drops its trailing
// findings of the largest file, with a note pointing at the full report.
//
// --stale takes a list of files (one per line, as `scripts/mutation-batch.sh
// --stale` prints them) whose results are out of date: their source or specs
// changed after the run that recorded them. Their mutants are left out of the
// score and the checklist, since the line numbers no longer match the source
// and the fix may already be in; they're only named, with their old count.
//
// --plan-sync <issues.json> decides what to do with the rolling issues: the
// input is the merged open + closed list of `mutation-testing` issues (as
// `gh api` prints it, pull requests are ignored), the manifest and bodies come
// from --out-dir. It prints the plan (create / edit / reopen+edit / close /
// skip) as JSON; with --dry-run it prints one readable line per action
// instead. The workflow only executes the plan, no matching logic lives in
// shell.
//
// Exits 0 either way; the body says when there's nothing to fix. When
// GITHUB_REPOSITORY and GITHUB_SHA are set, locations link to the file at that
// commit; REPORT_PATH_PREFIX (default src/frontend/buddy/) maps report paths to
// repo paths.
import { mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const MAX_FILES_PER_GROUP = 5;
export const DEFAULT_MAX_CHARS = 60000;
export const LABEL = 'mutation-testing';
export const TITLE_PREFIX = 'Frontend mutation testing findings';

const FINDINGS = new Set(['Survived', 'NoCoverage']);
const APP_PREFIX = 'src/app/';

const byPath = (a, b) => {
  if (a < b) return -1;
  return a > b ? 1 : 0;
};

// 32-bit FNV-1a over the UTF-8 bytes: tiny, deterministic across Node versions.
export function fnv1a32(text) {
  let hash = 0x811c9dc5;
  for (const byte of Buffer.from(text, 'utf8')) {
    hash ^= byte;
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash >>> 0;
}

export function folderOf(path) {
  const slash = path.lastIndexOf('/');
  const dir = slash === -1 ? '' : path.slice(0, slash);
  if (dir === 'src/app' || dir === '') return 'app';
  return dir.startsWith(APP_PREFIX) ? dir.slice(APP_PREFIX.length) : dir;
}

// Number of slots for a folder with `count` report files: the smallest power of
// two >= ceil(count / 4), so every slot holds about 4 files on average.
export function slotCount(count) {
  let slots = 1;
  while (slots < Math.ceil(count / 4)) slots *= 2;
  return slots;
}

// Bucket key of one file: the folder when it has at most 5 report files,
// otherwise `<folder>.<slot>` with a 2-digit slot taken from the path hash.
export function groupKey(path, folderCounts) {
  const folder = folderOf(path);
  const count = folderCounts.get(folder) ?? 1;
  if (count <= MAX_FILES_PER_GROUP) return folder;
  const slot = fnv1a32(path) % slotCount(count);
  return `${folder}.${String(slot).padStart(2, '0')}`;
}

// Splits the (sorted) report files of one bucket in chunks of at most 5.
export function chunkOverflow(bucketFiles) {
  const sorted = [...bucketFiles].sort(byPath);
  const chunks = [];
  for (let i = 0; i < sorted.length; i += MAX_FILES_PER_GROUP)
    chunks.push(sorted.slice(i, i + MAX_FILES_PER_GROUP));
  return chunks;
}

// a, b, ... z, aa, ab, ...
function chunkLetters(index) {
  let s = '';
  for (let i = index + 1; i > 0; i = Math.floor((i - 1) / 26))
    s = String.fromCodePoint(97 + ((i - 1) % 26)) + s;
  return s;
}

// Assigns every report file (with or without findings, stale included) to a
// group. Depends on the list of paths only, never on the surviving mutants.
// Returns Map<key, sortedPaths>, ordered by key.
export function assignGroups(allPaths) {
  const paths = [...new Set(allPaths)].sort(byPath);
  const folderCounts = new Map();
  for (const path of paths) {
    const folder = folderOf(path);
    folderCounts.set(folder, (folderCounts.get(folder) ?? 0) + 1);
  }
  const buckets = new Map();
  for (const path of paths) {
    const key = groupKey(path, folderCounts);
    if (!buckets.has(key)) buckets.set(key, []);
    buckets.get(key).push(path);
  }
  const groups = new Map();
  for (const [bucketKey, files] of buckets) {
    const chunks = chunkOverflow(files);
    chunks.forEach((chunk, i) =>
      groups.set(chunks.length > 1 ? `${bucketKey}.${chunkLetters(i)}` : bucketKey, chunk),
    );
  }
  return new Map([...groups].sort(([a], [b]) => byPath(a, b)));
}

// The key sits in an HTML comment: `--` or `>` would close it early.
export function sanitizeKey(key) {
  return key.replace(/-{2,}/g, '-').replaceAll('>', '_');
}

export const groupMarker = (key) => `<!-- mutation-group: ${sanitizeKey(key)} -->`;
export const groupTitle = (key) => `${TITLE_PREFIX} (${key})`;

export function groupSlug(key) {
  return key.replace(/[^A-Za-z0-9._-]+/g, '-');
}

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

function renderItem(m, links) {
  const line = m.location.start.line;
  const where = links
    ? `[L${line}](${links.server}/${links.repo}/blob/${links.sha}/${links.prefix}${links.path}#L${line})`
    : `L${line}`;
  const tag = m.status === 'NoCoverage' ? ' *(no coverage)*' : '';
  return `- [ ] ${where}: \`${m.mutatorName}\`${tag}: ${code(m.original)} → ${code(m.replacement ?? '')}`;
}

// `shown` findings of the file are listed; the rest is pointed at in the note.
function renderSection({ path, items }, shown = items.length) {
  const cut = items.length - shown;
  const more = cut === 1 ? 'finding of this file is' : 'findings of this file are';
  const note =
    cut > 0
      ? `\n> ${cut} more ${more} in ` +
        "`mutation-findings.md` in the run's `frontend-mutation-report` artifact.\n"
      : '';
  return `### \`${path}\` (${items.length})\n\n${items.slice(0, shown).join('\n')}\n${note}`;
}

function outdatedNote(outdated) {
  if (outdated.length === 0) return '';
  // Plain bullets, never checkboxes: these files are re-run on a later night.
  return (
    `> [!NOTE]\n> ${outdated.length} file(s) changed since their last mutation run and are waiting to be re-run, ` +
    'so their old findings are left out of the score and the list:\n' +
    outdated.map(({ path, old }) => `> - \`${path}\` (${old} before the change)`).join('\n') +
    '\n\n'
  );
}

// Stable part of a group's body: marker plus file sections. Depends only on the
// group's files and their findings. `group.sections` are rendered lines
// ({ path, items, shown }).
export function buildGroupStable(group) {
  const sections = group.sections.map((s) => renderSection(s, s.shown ?? s.items.length));
  return `${groupMarker(group.key)}\n\n${sections.join('\n')}`;
}

// Header part: changes every run and when other groups change.
export function buildGroupHeader(group, ctx) {
  const findings = group.sections.reduce((sum, s) => sum + s.items.length, 0);
  return [
    ctx.scoreLine,
    '',
    ctx.generated,
    '',
    ctx.howTo,
    '',
    `Group \`${group.key}\` of ${ctx.groupCount} findings issues ` +
      `(${group.sections.length} file(s), ${findings} finding(s) in this issue).`,
    '',
    outdatedNote(group.outdated ?? []),
  ].join('\n');
}

// Drops trailing findings of the largest file until the body fits maxChars.
export function buildGroupBody(group, ctx, maxChars = DEFAULT_MAX_CHARS) {
  const sections = group.sections.map((s) => ({ ...s, shown: s.items.length }));
  const build = () => {
    const g = { ...group, sections };
    return `${buildGroupHeader(g, ctx)}${buildGroupStable(g)}`;
  };
  let body = build();
  while (body.length > maxChars) {
    let target;
    let targetSize = -1;
    for (const s of sections) {
      if (s.shown === 0) continue;
      const size = renderSection(s, s.shown).length;
      // Sections are sorted by path, so ties go to the first path.
      if (size > targetSize) {
        target = s;
        targetSize = size;
      }
    }
    if (!target) break; // nothing left to drop
    target.shown--;
    body = build();
  }
  return body;
}

// Per-status mutant counts, the rendered findings of every live file, and the
// stale files that still had findings before the change.
function collectFindings(report, stale, linkBase) {
  const counts = {};
  const files = [];
  const outdated = [];
  for (const [path, file] of Object.entries(report.files ?? {}).sort(([a], [b]) => byPath(a, b))) {
    if (stale.has(path)) {
      const old = file.mutants.filter((m) => FINDINGS.has(m.status)).length;
      if (old > 0) outdated.push({ path, old });
      continue;
    }
    const lines = file.source.split('\n');
    const findings = [];
    for (const mutant of file.mutants) {
      counts[mutant.status] = (counts[mutant.status] ?? 0) + 1;
      if (FINDINGS.has(mutant.status))
        findings.push({ ...mutant, original: snippet(lines, mutant.location) });
    }
    if (findings.length === 0) continue;
    findings.sort(
      (a, b) =>
        a.location.start.line - b.location.start.line ||
        a.location.start.column - b.location.start.column,
    );
    const links = linkBase ? { ...linkBase, path } : null;
    files.push({ path, items: findings.map((m) => renderItem(m, links)) });
  }
  return { counts, files, outdated };
}

// Groups: layout comes from every report file, findings only fill it in.
function buildGroupDefs(paths, files, outdated) {
  const withFindings = new Map(files.map((f) => [f.path, f]));
  const outdatedByPath = new Map(outdated.map((o) => [o.path, o]));
  const groupDefs = [];
  for (const [key, groupPaths] of assignGroups(paths)) {
    const sections = groupPaths.filter((p) => withFindings.has(p)).map((p) => withFindings.get(p));
    if (sections.length === 0) continue;
    groupDefs.push({
      key,
      sections,
      outdated: groupPaths.filter((p) => outdatedByPath.has(p)).map((p) => outdatedByPath.get(p)),
    });
  }
  return groupDefs;
}

// Pure core: report JSON -> full combined body and per-group bodies.
export function buildIssues(report, opts = {}) {
  const {
    stale = new Set(),
    maxChars = DEFAULT_MAX_CHARS,
    repo,
    sha,
    server = 'https://github.com',
    prefix = 'src/frontend/buddy/',
    runId,
  } = opts;
  const linkBase = repo && sha ? { server, repo, sha, prefix } : null;
  const { counts, files, outdated } = collectFindings(report, stale, linkBase);

  const n = (status) => counts[status] ?? 0;
  const detected = n('Killed') + n('Timeout');
  const valid = detected + n('Survived') + n('NoCoverage');
  const score = valid > 0 ? `${((detected * 100) / valid).toFixed(2)}%` : 'n/a';
  const total = files.reduce((sum, f) => sum + f.items.length, 0);
  const runLink = runId ? ` by [this run](${server}/${repo}/actions/runs/${runId})` : '';

  const scoreLine =
    `Mutation score: **${score}** · ${n('Survived')} survived · ${n('NoCoverage')} without coverage · ` +
    `${n('Killed')} killed · ${n('Timeout')} timed out · ${n('CompileError')} compile errors`;
  const generated = sha
    ? `Generated from \`${sha.slice(0, 7)}\`${runLink}. ` +
      'This issue is rewritten by every nightly run, so ticked boxes are not kept; a fixed mutant simply disappears.'
    : '';
  const howTo =
    'Each entry is `line: Mutator` followed by the original code and the replacement that no test noticed. ' +
    'Fix a finding by adding or tightening a test so it fails for the replacement; ' +
    '[equivalent mutants](https://stryker-mutator.io/docs/mutation-testing-elements/equivalent-mutants/) can be ignored with a `// Stryker disable next-line <Mutator>: <reason>` comment.';
  const ctxBase = { scoreLine, generated, howTo };

  // Full combined body: every file with findings, never truncated.
  const empty = total === 0 ? 'No surviving or uncovered mutants. 🎉\n' : '';
  const intro = `${[scoreLine, '', generated, '', howTo, ''].join('\n')}\n${outdatedNote(outdated)}${empty}`;
  const full = `${intro}${files.map((f) => renderSection(f)).join('\n')}`;

  const groupDefs = buildGroupDefs(Object.keys(report.files ?? {}), files, outdated);
  const ctx = { ...ctxBase, groupCount: groupDefs.length };
  const groups = groupDefs.map((g) => ({
    key: g.key,
    title: groupTitle(g.key),
    marker: groupMarker(g.key),
    files: g.sections.map((s) => s.path),
    findings: g.sections.reduce((sum, s) => sum + s.items.length, 0),
    stable: buildGroupStable(g),
    header: buildGroupHeader(g, ctx),
    body: buildGroupBody(g, ctx, maxChars),
  }));

  return { score, total, full, groups };
}

export function writeOutDir(groups, dir) {
  mkdirSync(dir, { recursive: true });
  const manifest = { label: LABEL, groups: [] };
  groups.forEach((g, i) => {
    const file = `${String(i).padStart(3, '0')}-${groupSlug(g.key)}.md`;
    writeFileSync(join(dir, file), g.body);
    manifest.groups.push({ key: g.key, title: g.title, file, findings: g.findings });
  });
  writeFileSync(join(dir, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  return manifest;
}

const MARKER_RE = /<!-- mutation-group: ([^\r\n]+?) -->/;
const normalizeNewlines = (text) => (text ?? '').replaceAll('\r\n', '\n');

// Key inside the hidden marker of an issue body, or null when there is none
// (legacy issue) or it is malformed or foreign.
export function markerKeyOf(body) {
  const match = MARKER_RE.exec(normalizeNewlines(body));
  return match ? match[1] : null;
}

// Decides what the nightly run does with the rolling issues.
// manifest: { groups: [{ key, title, body, file? }] } (groups with findings only).
// existingIssues: [{ number, title, state, body, createdAt }], open and closed.
// Returns actions: { type: 'create' | 'edit' | 'reopen+edit' | 'close' | 'skip',
//                    key, number?, title?, file?, reason }.
// Matching is by the sanitised marker key, compared as a whole (`core` never
// matches `core.12`). Open beats closed; of several open issues the lowest
// number wins and the others are left alone; of several closed issues the
// newest is reopened. Issues without a marker (legacy) are never touched.
export function planSync(manifest, existingIssues) {
  const marked = [];
  for (const issue of existingIssues ?? []) {
    if (!issue || 'pull_request' in issue) continue;
    const key = markerKeyOf(issue.body);
    if (key === null) continue;
    marked.push({ ...issue, markerKey: key, open: String(issue.state).toLowerCase() === 'open' });
  }
  const wanted = new Set();
  const actions = [];
  for (const group of manifest.groups) {
    const markerKey = sanitizeKey(group.key);
    wanted.add(markerKey);
    const own = marked.filter((i) => i.markerKey === markerKey);
    const open = own.filter((i) => i.open).sort((a, b) => a.number - b.number);
    const closed = own
      .filter((i) => !i.open)
      .sort(
        (a, b) => String(b.createdAt).localeCompare(String(a.createdAt)) || b.number - a.number,
      );
    const base = { key: group.key, title: group.title, file: group.file };
    if (open.length > 0) {
      const issue = open[0];
      const same =
        normalizeNewlines(issue.body) === normalizeNewlines(group.body) &&
        issue.title === group.title;
      actions.push(
        same
          ? { ...base, type: 'skip', number: issue.number, reason: 'issue is up to date' }
          : { ...base, type: 'edit', number: issue.number, reason: 'refresh findings' },
      );
    } else if (closed.length > 0) {
      actions.push({
        ...base,
        type: 'reopen+edit',
        number: closed[0].number,
        reason: 'findings are back',
      });
    } else {
      actions.push({ ...base, type: 'create', reason: 'no issue for this group yet' });
    }
  }
  const stale = marked
    .filter((i) => i.open && !wanted.has(i.markerKey))
    .sort((a, b) => a.number - b.number);
  for (const issue of stale)
    actions.push({
      type: 'close',
      key: issue.markerKey,
      number: issue.number,
      reason: 'no findings left in this group',
    });
  return actions;
}

const issueRef = (number) => (number ? ` #${number}` : '');

export function formatPlan(actions) {
  return actions.map((a) => `${a.type}${issueRef(a.number)} ${a.key}: ${a.reason}`).join('\n');
}

function readManifestDir(dir) {
  const manifest = JSON.parse(readFileSync(join(dir, 'manifest.json'), 'utf8'));
  return {
    ...manifest,
    groups: manifest.groups.map((g) => ({ ...g, body: readFileSync(join(dir, g.file), 'utf8') })),
  };
}

function main(argv) {
  const args = [...argv];
  const flag = (name, fallback) => {
    const i = args.indexOf(name);
    if (i === -1) return fallback;
    const [, value] = args.splice(i, 2);
    return value;
  };
  const dryRun = args.includes('--dry-run');
  if (dryRun) args.splice(args.indexOf('--dry-run'), 1);
  const planFile = flag('--plan-sync');
  const outFile = flag('--out');
  const outDir = flag('--out-dir');
  const maxChars = Number(flag('--max-chars', String(DEFAULT_MAX_CHARS)));
  if (planFile) {
    if (!outDir) throw new Error('--plan-sync needs --out-dir with the manifest and bodies');
    const issues = JSON.parse(readFileSync(planFile, 'utf8'));
    const actions = planSync(readManifestDir(outDir), issues);
    process.stdout.write(
      dryRun ? `${formatPlan(actions)}\n` : `${JSON.stringify(actions, null, 2)}\n`,
    );
    return;
  }
  const staleFile = flag('--stale');
  const reportPath = args[0] ?? 'reports/stryker-incremental.json';
  const stale = new Set(
    staleFile
      ? readFileSync(staleFile, 'utf8')
          .split('\n')
          .map((line) => line.trim())
          .filter(Boolean)
      : [],
  );
  const report = JSON.parse(readFileSync(reportPath, 'utf8'));
  const env = process.env;
  const { full, groups } = buildIssues(report, {
    stale,
    maxChars,
    repo: env.GITHUB_REPOSITORY,
    sha: env.GITHUB_SHA,
    server: env.GITHUB_SERVER_URL || undefined,
    prefix: env.REPORT_PATH_PREFIX ?? undefined,
    runId: env.GITHUB_RUN_ID,
  });
  if (outFile) writeFileSync(outFile, full);
  if (outDir) writeOutDir(groups, outDir);
  process.stdout.write(full);
}

// Run as a CLI only when executed directly (also through a symlink), not when
// the test file imports the module.
const isCli = (() => {
  try {
    return realpathSync(process.argv[1]) === realpathSync(fileURLToPath(import.meta.url));
  } catch {
    return false;
  }
})();
if (isCli) main(process.argv.slice(2));
