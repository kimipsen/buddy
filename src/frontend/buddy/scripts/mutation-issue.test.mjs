import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import {
  mkdtempSync,
  readdirSync,
  readFileSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  assignGroups,
  buildIssues,
  chunkOverflow,
  fnv1a32,
  groupMarker,
  groupSlug,
  groupTitle,
  markerKeyOf,
  planSync,
  sanitizeKey,
  writeOutDir,
} from './mutation-issue.mjs';

const SCRIPT = fileURLToPath(new URL('./mutation-issue.mjs', import.meta.url));

function mutant(id, status, line = 1) {
  return {
    id: String(id),
    mutatorName: 'ConditionalExpression',
    status,
    replacement: 'false',
    location: { start: { line, column: 1 }, end: { line, column: 5 } },
  };
}

// `findings` = number of surviving mutants; one extra killed mutant per file
// keeps the score defined.
function file(findings, extra = []) {
  const mutants = [mutant('k', 'Killed')];
  for (let i = 0; i < findings; i++)
    mutants.push(mutant(i, i % 2 ? 'NoCoverage' : 'Survived', i + 1));
  return { source: 'true && x;\n'.repeat(Math.max(findings, 1)), mutants: [...mutants, ...extra] };
}

const app = (rel) => `src/app/${rel}`;
const report = (files) => ({ files });
const names = (folder, count) =>
  Array.from({ length: count }, (_, i) => app(`${folder}/f${String(i).padStart(2, '0')}.ts`));

function reportOf(paths, findingsOf = () => 1) {
  return report(Object.fromEntries(paths.map((p) => [p, file(findingsOf(p))])));
}

const keyOfFile = (groups) => {
  const map = new Map();
  for (const [key, paths] of groups) for (const p of paths) map.set(p, key);
  return map;
};

test('cap: a folder of 40 files with findings yields groups of at most 5 files', () => {
  const { groups } = buildIssues(reportOf(names('core', 40)));
  assert.ok(groups.length >= 8);
  for (const g of groups) assert.ok(g.files.length <= 5, `${g.key} has ${g.files.length}`);
  assert.equal(groups.flatMap((g) => g.files).length, 40);
});

test('a folder with at most 5 files is one group keyed by the folder', () => {
  const layout = assignGroups(names('features/guardian/night-duty', 5));
  assert.deepEqual([...layout.keys()], ['features/guardian/night-duty']);
});

test('root src/app files get the key app', () => {
  const layout = assignGroups(['src/app/app.ts', 'src/app/app.routes.ts']);
  assert.deepEqual([...layout.keys()], ['app']);
});

test('an empty report yields no groups and an empty manifest', () => {
  const dir = mkdtempSync(join(tmpdir(), 'mi-'));
  try {
    const { groups } = buildIssues(report({}));
    assert.deepEqual(groups, []);
    assert.deepEqual(writeOutDir(groups, dir).groups, []);
    assert.deepEqual(readdirSync(dir), ['manifest.json']);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('identical input twice gives identical keys and bodies', () => {
  const r = reportOf(names('core', 30));
  const a = buildIssues(r);
  const b = buildIssues(r);
  assert.deepEqual(
    a.groups.map((g) => [g.key, g.body]),
    b.groups.map((g) => [g.key, g.body]),
  );
});

// Twelve paths that all hash into slot 0 of 4, so they form one overflowing bucket.
function collidingPaths() {
  const out = [];
  for (let i = 0; out.length < 12; i++) {
    const p = app(`x/c${i}.ts`);
    if (fnv1a32(p) % 4 === 0) out.push(p);
  }
  return out;
}

test('overflow: chunks stay at most 5 and fixing one file moves nothing else', () => {
  const paths = collidingPaths();
  const layout = assignGroups(paths);
  assert.equal(layout.size, 3);
  for (const files of layout.values()) assert.ok(files.length <= 5);
  assert.deepEqual([...layout.keys()], ['x.00.a', 'x.00.b', 'x.00.c']);

  const before = buildIssues(reportOf(paths, () => 2));
  const fixed = paths[0];
  const after = buildIssues(reportOf(paths, (p) => (p === fixed ? 0 : 2)));
  const keys = keyOfFile(assignGroups(paths));
  const stableBefore = new Map(before.groups.map((g) => [g.key, g.stable]));
  for (const g of after.groups) {
    for (const p of g.files) assert.equal(keys.get(p), g.key);
    if (keys.get(fixed) !== g.key && stableBefore.has(g.key)) {
      assert.equal(g.stable, stableBefore.get(g.key), `${g.key} stable part changed`);
    }
  }
  assert.ok(after.groups.every((g) => !g.files.includes(fixed)));
});

test('stability under fixes: other groups keep their stable part byte for byte', () => {
  const paths = names('core', 40);
  const before = buildIssues(reportOf(paths, () => 3));
  const fixedSome = new Set(paths.slice(0, 10));
  const after = buildIssues(reportOf(paths, (p) => (fixedSome.has(p) ? 0 : 3)));
  const beforeByKey = new Map(before.groups.map((g) => [g.key, g]));
  for (const g of after.groups) {
    const old = beforeByKey.get(g.key);
    assert.ok(old, `${g.key} is new`);
    const untouched = !g.files.some((p) => old.files.some((o) => fixedSome.has(o)));
    if (untouched) assert.equal(g.stable, old.stable);
    for (const p of g.files) assert.ok(old.files.includes(p), `${p} moved`);
  }
});

test('adding a file keeps unrelated files in their group', () => {
  const paths = names('core', 40);
  const extra = app('core/zz-new.ts');
  const before = keyOfFile(assignGroups(paths));
  const after = keyOfFile(assignGroups([...paths, extra]));
  const base = (key) => key.replace(/\.[a-z]+$/, '');
  for (const p of paths) {
    if (before.get(p) !== after.get(p)) assert.equal(base(before.get(p)), base(after.get(extra)));
  }
});

test('removing a file keeps unrelated files in their group', () => {
  const paths = names('core', 40);
  const before = keyOfFile(assignGroups(paths));
  const after = keyOfFile(assignGroups(paths.slice(1)));
  const base = (key) => key.replace(/\.[a-z]+$/, '');
  for (const p of paths.slice(1)) {
    if (before.get(p) !== after.get(p))
      assert.equal(base(before.get(p)), base(before.get(paths[0])));
  }
});

test('crossing a power-of-two slot threshold re-keys files (documented limitation)', () => {
  const sixteen = keyOfFile(assignGroups(names('y', 16)));
  const seventeen = keyOfFile(assignGroups(names('y', 17)));
  const moved = [...sixteen].filter(([p, k]) => seventeen.get(p) !== k);
  assert.ok(moved.length > 0);
});

test('a folder growing past 5 files switches to slotted keys once', () => {
  assert.deepEqual([...assignGroups(names('z', 5)).keys()], ['z']);
  assert.ok([...assignGroups(names('z', 6)).keys()].every((k) => /^z\.\d\d(\.[a-z]+)?$/.test(k)));
});

test('a group without findings is absent from the manifest and writes no file', () => {
  const paths = names('core', 12);
  const withFinding = paths[0];
  const { groups } = buildIssues(reportOf(paths, (p) => (p === withFinding ? 1 : 0)));
  assert.equal(groups.length, 1);
  const dir = mkdtempSync(join(tmpdir(), 'mi-'));
  try {
    const manifest = writeOutDir(groups, dir);
    assert.equal(manifest.groups.length, 1);
    assert.equal(readdirSync(dir).length, 2); // manifest.json + one body
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('every file with findings appears in exactly one group; no "did not fit" text', () => {
  const paths = names('core', 74);
  const { groups, full } = buildIssues(reportOf(paths, () => 4));
  const seen = groups.flatMap((g) => g.files);
  assert.equal(seen.length, 74);
  assert.equal(new Set(seen).size, 74);
  assert.ok(!full.includes('did not fit'));
  for (const g of groups) {
    assert.ok(!g.body.includes('did not fit'));
    for (const p of g.files) assert.equal(g.body.split(`### \`${p}\``).length - 1, 1);
  }
});

test('safety net: a body over --max-chars is cut deterministically with a note', () => {
  const big = app('f/big.ts');
  const small = app('f/small.ts');
  const r = report({ [big]: file(200), [small]: file(2) });
  const a = buildIssues(r, { maxChars: 6000 });
  const b = buildIssues(r, { maxChars: 6000 });
  const [group] = a.groups;
  assert.ok(group.body.length <= 6000, `${group.body.length}`);
  assert.equal(group.body, b.groups[0].body);
  assert.match(group.body, /more findings? of this file (is|are) in `mutation-findings\.md`/);
  assert.equal(group.body.split('more finding').length - 1, 1);
  // The small file is untouched: all its findings are still listed.
  const smallSection = group.body.split(`### \`${small}\``)[1];
  assert.equal(smallSection.split('- [ ]').length - 1, 2);
  // The full output keeps everything.
  assert.equal(a.full.split('- [ ]').length - 1, 202);
});

test('safety net: one file with thousands of findings still fits', () => {
  const r = report({ [app('f/huge.ts')]: file(3000) });
  const { groups } = buildIssues(r, { maxChars: 60000 });
  assert.ok(groups[0].body.length <= 60000);
  assert.match(groups[0].body, /more findings of this file are in/);
});

test('stale-only group is absent; a stale file in a group with findings is a plain bullet', () => {
  const live = app('a/live.ts');
  const staleInGroup = app('a/stale.ts');
  const staleOnly = app('b/only.ts');
  const r = report({ [live]: file(2), [staleInGroup]: file(3), [staleOnly]: file(4) });
  const { groups, full } = buildIssues(r, { stale: new Set([staleInGroup, staleOnly]) });
  assert.deepEqual(
    groups.map((g) => g.key),
    ['a'],
  );
  const body = groups[0].body;
  assert.match(
    body,
    new RegExp(`> - \`${staleInGroup.replace(/[/.]/g, '\\$&')}\` \\(3 before the change\\)`),
  );
  assert.ok(!body.includes(`### \`${staleInGroup}\``));
  for (const line of body.split('\n').filter((l) => l.startsWith('>')))
    assert.ok(!line.includes('- [ ]'));
  assert.ok(!full.includes(`### \`${staleInGroup}\``));
});

test('marker and title format; totals only in the body', () => {
  const { groups } = buildIssues(reportOf(names('shared/ui', 8)));
  for (const g of groups) {
    assert.equal(g.title, `Frontend mutation testing findings (${g.key})`);
    assert.match(g.key, /^shared\/ui\.\d\d(\.[a-z])?$/);
    assert.ok(g.body.startsWith(groupMarker(g.key)) === false); // header first
    assert.ok(g.body.includes(`<!-- mutation-group: ${g.key} -->`));
    assert.ok(g.stable.startsWith(`<!-- mutation-group: ${g.key} -->`));
    assert.ok(!/\d+ findings issues/.test(g.title));
    assert.match(g.body, /of \d+ findings issues/);
  }
});

test('marker sanitising never closes the comment early', () => {
  const key = 'a--b>c---d';
  const marker = groupMarker(key);
  assert.equal(sanitizeKey(key), 'a-b_c-d');
  assert.equal(marker, '<!-- mutation-group: a-b_c-d -->');
  assert.equal(marker.slice(4, -3).includes('--'), false);
  assert.equal(marker.slice(0, -3).includes('>'), false);
  assert.equal(groupTitle('x.00'), 'Frontend mutation testing findings (x.00)');
});

test('slug collisions are avoided by the index prefix', () => {
  assert.equal(groupSlug('a/b.01'), groupSlug('a-b.01'));
  const dir = mkdtempSync(join(tmpdir(), 'mi-'));
  try {
    const mk = (key) => ({ key, title: groupTitle(key), body: key, findings: 1 });
    const manifest = writeOutDir([mk('a-b.01'), mk('a/b.01')], dir);
    assert.notEqual(manifest.groups[0].file, manifest.groups[1].file);
    assert.match(manifest.groups[0].file, /^000-/);
    assert.match(manifest.groups[1].file, /^001-/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('chunkOverflow sorts by path and chunks in fives', () => {
  const chunks = chunkOverflow(['e', 'd', 'c', 'b', 'a', 'h', 'g', 'f']);
  assert.deepEqual(chunks, [
    ['a', 'b', 'c', 'd', 'e'],
    ['f', 'g', 'h'],
  ]);
});

test('--out-dir CLI writes manifest and bodies, also through a symlink', () => {
  const dir = mkdtempSync(join(tmpdir(), 'mi-'));
  try {
    const reportPath = join(dir, 'report.json');
    writeFileSync(reportPath, JSON.stringify(reportOf([...names('core', 7), app('a/one.ts')])));
    const link = join(dir, 'link.mjs');
    symlinkSync(SCRIPT, link);
    for (const [script, out] of [
      [SCRIPT, join(dir, 'out-direct')],
      [link, join(dir, 'out-link')],
    ]) {
      const stdout = execFileSync(process.execPath, [script, reportPath, '--out-dir', out], {
        encoding: 'utf8',
      });
      assert.match(stdout, /Mutation score/);
      const manifest = JSON.parse(readFileSync(join(out, 'manifest.json'), 'utf8'));
      assert.equal(manifest.label, 'mutation-testing');
      assert.ok(manifest.groups.length >= 3);
      for (const [i, g] of manifest.groups.entries()) {
        assert.match(g.file, new RegExp(`^${String(i).padStart(3, '0')}-.+\\.md$`));
        assert.ok(
          readFileSync(join(out, g.file), 'utf8').includes(`<!-- mutation-group: ${g.key} -->`),
        );
      }
      assert.equal(readdirSync(out).length, manifest.groups.length + 1);
    }
    assert.deepEqual(
      readdirSync(join(dir, 'out-direct')).map((f) =>
        readFileSync(join(dir, 'out-direct', f), 'utf8'),
      ),
      readdirSync(join(dir, 'out-link')).map((f) => readFileSync(join(dir, 'out-link', f), 'utf8')),
    );
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// --- planSync ---

const mf = (...keys) => ({
  groups: keys.map((key) => ({
    key,
    title: groupTitle(key),
    body: `${groupMarker(key)}\nbody ${key}\n`,
  })),
});
const issue = (number, key, state = 'open', extra = {}) => ({
  number,
  title: groupTitle(key),
  state,
  body: `${groupMarker(key)}\nold\n`,
  createdAt: `2026-11-${String(number).padStart(2, '0')}T00:00:00Z`,
  ...extra,
});
const typeOf = (actions, key) => actions.filter((a) => a.key === key).map((a) => a.type);

test('planSync: creates a missing group and edits an existing open one', () => {
  const actions = planSync(mf('a', 'b'), [issue(7, 'a')]);
  assert.deepEqual(typeOf(actions, 'a'), ['edit']);
  assert.equal(actions.find((a) => a.key === 'a').number, 7);
  assert.deepEqual(typeOf(actions, 'b'), ['create']);
});

test('planSync: open beats closed, so no duplicate is created', () => {
  const actions = planSync(mf('a'), [issue(3, 'a', 'closed'), issue(9, 'a', 'open')]);
  assert.deepEqual(
    actions.map((a) => [a.type, a.number]),
    [['edit', 9]],
  );
});

test('planSync: the newest closed issue is reopened', () => {
  const actions = planSync(mf('a'), [
    issue(3, 'a', 'closed'),
    issue(5, 'a', 'CLOSED'),
    issue(4, 'a', 'closed'),
  ]);
  assert.deepEqual(
    actions.map((a) => [a.type, a.number]),
    [['reopen+edit', 5]],
  );
});

test('planSync: the lowest open number wins and duplicates are left alone', () => {
  const actions = planSync(mf('a'), [issue(8, 'a'), issue(2, 'a'), issue(5, 'a')]);
  assert.deepEqual(
    actions.map((a) => [a.type, a.number]),
    [['edit', 2]],
  );
});

test('planSync: open marked issues without findings are closed, closed ones ignored', () => {
  const actions = planSync(mf('a'), [issue(1, 'a'), issue(2, 'gone'), issue(3, 'older', 'closed')]);
  assert.deepEqual(
    actions.filter((a) => a.type === 'close').map((a) => [a.key, a.number]),
    [['gone', 2]],
  );
  assert.equal(actions.length, 2);
});

test('planSync: marker-less issues (#101) and pull requests are never touched', () => {
  const legacy = {
    number: 101,
    title: 'Frontend mutation testing findings',
    state: 'open',
    body: 'no marker',
    createdAt: '2026-11-01T00:00:00Z',
  };
  const pr = { ...issue(50, 'gone'), pull_request: {} };
  assert.deepEqual(planSync(mf(), [legacy, pr, { ...legacy, number: 102, body: null }]), []);
});

test('planSync: the marker key is matched whole, never as a prefix', () => {
  const actions = planSync(mf('core', 'a/b'), [issue(12, 'core.12'), issue(13, 'a/b.00')]);
  assert.deepEqual(typeOf(actions, 'core'), ['create']);
  assert.deepEqual(typeOf(actions, 'a/b'), ['create']);
  assert.deepEqual(
    actions.filter((a) => a.type === 'close').map((a) => a.number),
    [12, 13],
  );
});

test('planSync: a second run on the same input creates nothing', () => {
  const m = mf('a', 'b');
  const issues = m.groups.map((g, i) => ({ ...issue(i + 1, g.key), body: g.body }));
  const actions = planSync(m, issues);
  assert.ok(actions.every((a) => a.type === 'skip'));
});

test('planSync: an equal body (also with CRLF) is skipped, a different one is edited', () => {
  const m = mf('a');
  const crlf = m.groups[0].body.replace(/\n/g, '\r\n');
  assert.equal(planSync(m, [issue(1, 'a', 'open', { body: crlf })])[0].type, 'skip');
  assert.equal(planSync(m, [issue(1, 'a', 'open', { body: `${crlf}x` })])[0].type, 'edit');
  assert.equal(
    planSync(m, [issue(1, 'a', 'open', { title: 'renamed', body: crlf })])[0].type,
    'edit',
  );
  assert.equal(planSync(m, [issue(1, 'a', 'closed', { body: crlf })])[0].type, 'reopen+edit');
});

test('planSync: malformed or foreign markers are ignored without closing anything', () => {
  const bad = [
    '<!-- mutation-group: -->',
    '<!-- mutation-group:a -->',
    '<!-- mutation-group: a ->',
    '<!-- other-group: a -->',
    '<!-- mutation-group: a',
  ];
  const issues = bad.map((body, i) => ({
    number: i + 1,
    title: 't',
    state: 'open',
    body,
    createdAt: '2026-11-01T00:00:00Z',
  }));
  for (const i of issues) assert.equal(markerKeyOf(i.body), null);
  assert.deepEqual(
    planSync(mf('a'), issues).map((a) => a.type),
    ['create'],
  );
});

test('planSync: a marker key is found inside a longer body, CRLF included', () => {
  assert.equal(markerKeyOf(`header\r\n\r\n${groupMarker('x/y.01')}\r\n### f`), 'x/y.01');
});

test('--plan-sync CLI: CRLF bodies, a marker-less issue and two open duplicates', () => {
  const dir = mkdtempSync(join(tmpdir(), 'mi-'));
  try {
    const out = join(dir, 'out');
    const reportPath = join(dir, 'report.json');
    writeFileSync(
      reportPath,
      JSON.stringify(reportOf([app('a/one.ts'), app('b/two.ts'), app('c/three.ts')])),
    );
    execFileSync(process.execPath, [SCRIPT, reportPath, '--out-dir', out]);
    const manifest = JSON.parse(readFileSync(join(out, 'manifest.json'), 'utf8'));
    assert.deepEqual(
      manifest.groups.map((g) => g.key),
      ['a', 'b', 'c'],
    );
    const bodyOf = (key) =>
      readFileSync(join(out, manifest.groups.find((g) => g.key === key).file), 'utf8');
    const issues = [
      {
        number: 101,
        title: 'Frontend mutation testing findings',
        state: 'open',
        body: 'legacy',
        createdAt: '2026-11-01T00:00:00Z',
      },
      { ...issue(10, 'a'), body: bodyOf('a').replace(/\n/g, '\r\n') },
      issue(11, 'b'),
      issue(12, 'b'),
      issue(13, 'gone'),
      { ...issue(14, 'c', 'open'), pull_request: {} },
    ];
    const issuesPath = join(dir, 'issues.json');
    writeFileSync(issuesPath, JSON.stringify(issues));
    const run = (...extra) =>
      execFileSync(
        process.execPath,
        [SCRIPT, '--plan-sync', issuesPath, '--out-dir', out, ...extra],
        { encoding: 'utf8' },
      );
    const plan = JSON.parse(run());
    assert.deepEqual(
      plan.map((a) => [a.type, a.key, a.number ?? null]),
      [
        ['skip', 'a', 10],
        ['edit', 'b', 11],
        ['create', 'c', null],
        ['close', 'gone', 13],
      ],
    );
    assert.equal(plan.find((a) => a.type === 'create').file, manifest.groups[2].file);
    const dry = run('--dry-run');
    assert.match(dry, /^skip #10 a: /m);
    assert.match(dry, /^create c: /m);
    assert.match(dry, /^close #13 gone: /m);
    assert.ok(!dry.includes('"type"'));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
