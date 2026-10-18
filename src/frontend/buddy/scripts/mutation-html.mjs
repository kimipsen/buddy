#!/usr/bin/env node
// Renders a Stryker mutation report (mutation-testing-report-schema JSON) as
// the same single-file HTML report Stryker's html reporter writes. Batched
// runs (scripts/mutation-batch.sh) only get an HTML report of their last
// batch from Stryker; this one covers everything in the incremental file.
//
// Usage:
//   node scripts/mutation-html.mjs [report.json] [out.html]
// Defaults: reports/stryker-incremental.json -> reports/mutation/index.html
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname } from 'node:path';

const [reportPath = 'reports/stryker-incremental.json', outPath = 'reports/mutation/index.html'] =
  process.argv.slice(2);

const require = createRequire(import.meta.url);
// A dependency of @stryker-mutator/core, so resolve it from there.
const elements = require.resolve('mutation-testing-elements/dist/mutation-test-elements.js', {
  paths: [dirname(require.resolve('@stryker-mutator/core/package.json'))],
});
const script = readFileSync(elements, 'utf8');
// `<` keeps a `</script>` inside source code from ending the script tag.
const report = readFileSync(reportPath, 'utf8').trim().replaceAll('<', '\\u003c');

mkdirSync(dirname(outPath), { recursive: true });
writeFileSync(
  outPath,
  `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>Buddy frontend mutation report</title>
  <script>${script}</script>
</head>
<body>
  <mutation-test-report-app titlePostfix="Stryker"></mutation-test-report-app>
  <script>
    const app = document.querySelector('mutation-test-report-app');
    app.report = ${report};
    const updateTheme = () => (document.body.style.backgroundColor = app.themeBackgroundColor);
    app.addEventListener('theme-changed', updateTheme);
    updateTheme();
  </script>
</body>
</html>
`,
);
console.log(`Wrote ${outPath}`);
