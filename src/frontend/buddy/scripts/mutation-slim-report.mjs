#!/usr/bin/env node
// Shortens the statusReason of each mutant in a Stryker incremental report.
// With the command test runner, Stryker stores the whole `npm test` output of
// every run there, which grows reports/stryker-incremental.json past GitHub's
// 100 MB file limit. Only the end of that output (the failing tests and the
// summary) is worth keeping; Stryker doesn't read it when it reuses results.
// Short reasons, such as those of `// Stryker disable` comments, are kept as is.
//
// It also drops files that were deleted or are no longer in stryker.conf.json's
// `mutate` globs. Stryker never re-tests those, so their old results would stay
// in the score and the nightly findings issue for good.
//
// Usage:
//   node scripts/mutation-slim-report.mjs [report.json] [--max-chars N]
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { matchesGlob } from 'node:path';

const args = process.argv.slice(2);
const i = args.indexOf('--max-chars');
const maxChars = i === -1 ? 1000 : Number(args.splice(i, 2)[1]);
const path = args[0] ?? 'reports/stryker-incremental.json';

if (!existsSync(path)) process.exit(0);

const report = JSON.parse(readFileSync(path, 'utf8'));

const { mutate } = JSON.parse(readFileSync('stryker.conf.json', 'utf8'));
const included = mutate.filter((g) => !g.startsWith('!'));
const excluded = mutate.filter((g) => g.startsWith('!')).map((g) => g.slice(1));
const stale = Object.keys(report.files).filter(
  (file) =>
    !existsSync(file) ||
    !included.some((g) => matchesGlob(file, g)) ||
    excluded.some((g) => matchesGlob(file, g)),
);
for (const file of stale) delete report.files[file];
if (stale.length > 0)
  console.log(`Dropped ${stale.length} file(s) no longer mutated: ${stale.join(', ')}`);

let trimmed = 0;
for (const file of Object.values(report.files)) {
  for (const mutant of file.mutants) {
    const reason = mutant.statusReason;
    if (typeof reason === 'string' && reason.length > maxChars) {
      mutant.statusReason = `…${reason.slice(-maxChars)}`;
      trimmed++;
    }
  }
}
writeFileSync(path, JSON.stringify(report, null, 2));
console.log(`Shortened ${trimmed} status reason(s) in ${path} to ${maxChars} characters.`);
