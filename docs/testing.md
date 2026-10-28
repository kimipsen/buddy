# Testing

Buddy has frontend unit tests, backend integration and event-shape tests, and
nightly and manual mutation-testing workflows. Run commands from the repository root unless
a command changes directory explicitly.

## Frontend

Install dependencies once:

```bash
cd src/frontend/buddy
npm install
```

Run the Angular unit suite once with the Vitest-backed builder:

```bash
npm test -- --watch=false
```

`vitest-base.config.ts` (the builder's `runnerConfig`) caps Vitest at half the
cores and raises the per-test timeout to 15 seconds, so the `settle()`-based
specs don't time out when dev servers, language servers or Stryker load the
machine.

Run mutation testing with StrykerJS:

```bash
npm run test:mutation
```

The HTML mutation report is written to
`src/frontend/buddy/reports/mutation/index.html`. See the
[frontend overview](frontend/README.md#mutation-testing) for configuration and
resource considerations.

### End-to-end tests

Run the Playwright suite, which drives a real browser against the real
backend API and Keycloak instance (not mocked):

```bash
cd src/frontend/buddy
npx playwright install --with-deps chromium   # once per machine
npm run test:e2e
```

This needs Postgres, Keycloak, and Mailpit running -- inside the devcontainer
they already are, seeded automatically from
`.devcontainer/keycloak/buddy-realm.json` (see the
[development container guide](../.devcontainer/README.md)).
`playwright.config.ts` starts the Angular dev server and the backend API
itself, giving that API a larger anonymous rate-limit budget (1000 tokens, 100 per
second) because every logged-out request in the suite shares one localhost IP;
production limits are unchanged, and an already-running dev API keeps its own
limits. Most specs authenticate via a fast direct-grant token fetch
(`e2e/support/auth-fixture.ts`) rather than driving Keycloak's hosted login
form; `e2e/login.spec.ts` is the one spec that drives the real form, to prove
the redirect/PKCE flow itself still works.

### Documentation screenshots

`task docs:screenshots` (`npm run screenshots` in `src/frontend/buddy`) seeds a fresh demo family
and uses a separate Playwright config
(`src/frontend/buddy/playwright.screenshots.config.ts`) to capture every page listed in
`src/frontend/buddy/screenshots/pages.ts`, once at desktop size (1280×800) into `docs/screenshots/`
and once as an iPhone 15 into `docs/screenshots/mobile/`, regenerating
[docs/screenshots/README.md](screenshots/README.md). It needs the same Postgres, Keycloak, and
Mailpit prerequisites as the e2e suite.
`src/frontend/buddy/src/app/screenshot-coverage.spec.ts` runs as part of the regular Vitest suite
and fails when a route is missing from `pages.ts` (or its documented
`UNCAPTURED_ROUTES` exceptions). See the `doc-screenshots` skill for details.

### Waiting for async work in component tests

The frontend has no `zone.js` dependency and runs zoneless. Component tests
that use `fixture.whenStable()` to wait for a component's `ngOnInit` (or a
click handler) to finish its async work will find that it resolves
immediately and does nothing, because `ApplicationRef`'s stability tracking
only waits on `PendingTasks` entries -- things like in-flight `HttpClient`
requests -- and a plain `Promise` returned by a stubbed/mocked service is
never registered as one. Tests built against `provideHttpClient` +
`HttpTestingController` are unaffected, since `HttpClient` registers a
pending task per request; this only bites tests that stub the service layer
directly (the pattern used throughout this app's component specs).

The fix used across the child feature specs
([home.spec.ts](../src/frontend/buddy/src/app/features/child/home/home.spec.ts),
[child-mealplan.spec.ts](../src/frontend/buddy/src/app/features/child/mealplan/child-mealplan.spec.ts))
is a macrotask flush instead of `whenStable()`:

```ts
async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 0));
  fixture.detectChanges();
}
```

A `setTimeout` callback only runs after the microtask queue is fully drained,
so this reliably flushes any depth of chained `await`s in a mocked service
call (including a `Promise.all` of several mocked calls), as long as nothing
in that chain schedules a further macrotask itself.

### `window.matchMedia` in specs

jsdom doesn't implement `window.matchMedia`, which `ThemeService` calls on construction. A global
stub in
[`src/test-setup.ts`](../src/frontend/buddy/src/test-setup.ts) (wired up via `angular.json`'s
`test.options.setupFiles`) keeps any spec that injects `ThemeService` through real DI from
crashing with "matchMedia is not a function". Specs that care about a specific OS preference
(e.g. `theme.service.spec.ts`) stub `matchMedia` themselves instead of relying on the default.

## Backend

Build the complete backend solution:

```bash
dotnet build src/backend/backend.slnx --configuration Release
```

Run the full integration suite:

```bash
dotnet test src/backend/backend.slnx --configuration Release
```

The integration suite uses Testcontainers for PostgreSQL, Keycloak, and
Mailpit. A working Docker daemon is required. The development container and the
GitHub-hosted CI runner both provide one.

Run only the container-free event serialization checks:

```bash
dotnet test src/backend/backend.slnx \
  --filter FullyQualifiedName~EventShapeTests
```

Run a narrower test class or namespace with the standard .NET test filter:

```bash
dotnet test src/backend/backend.slnx \
  --filter FullyQualifiedName~Features.Pickups
```

The [integration testing strategy](backend/analysis/integration-testing-strategy.md)
documents the Testcontainers fixture, endpoint coverage guard, and event-shape
golden files.

## Backend mutation testing

Restore the repository-local Stryker.NET tool and run it from the integration
test project:

```bash
cd src/backend/buddy.IntegrationTests
dotnet tool restore
dotnet stryker
```

A full run is intentionally slow because mutants are checked against real
container-backed integration tests. Reports are written under
`src/backend/buddy.IntegrationTests/StrykerOutput/`. See the
[mutation testing strategy](backend/analysis/mutation-testing-strategy.md) for
verified scoping instructions and expected runtime characteristics.

## Load testing (k6)

Performance and load tests for the API live in `src/load-tests` and run with
[k6](https://grafana.com/docs/k6/latest/) against an isolated, production-sized
Compose stack (own Postgres and Keycloak, the API built from the working tree,
Grafana on <http://localhost:3300>):

```bash
task load:stack:up
task test:load                                  # smoke
task test:load PROFILE=load VUS=50 HOLD=10m     # or stress, spike, soak, breakpoint, rate-limit
task load:stack:down
```

`task test:load:typecheck` checks the scripts against the generated API types.
Pull requests run the type check and a smoke run in the e2e workflow; the
`Load tests (k6)` workflow runs smoke, load and rate-limit nightly and any
profile on demand. See [src/load-tests/README.md](../src/load-tests/README.md)
and the design in
[load-testing-k6.md](backend/analysis/load-testing-k6.md).

## Continuous integration

- `.github/workflows/backend-tests.yml` restores, builds, and runs the backend
  suite for backend changes.
- `.github/workflows/frontend-tests.yml` type-checks the app and spec
  tsconfigs, runs the Vitest unit suite, and runs the en/da translation parity
  check (`.claude/skills/i18n/check-parity.mjs`) for frontend changes.
- `.github/workflows/e2e-tests.yml` starts Postgres, Keycloak, and Mailpit via
  `.devcontainer/docker-compose.yml` and runs the Playwright suite against a
  real backend and frontend. A CI-only override (`docker-compose.ci.yml`,
  written by the workflow) publishes Postgres on `localhost:5432` for the API
  running on the runner.
- `.github/workflows/mutation-testing.yml` runs mutation testing nightly and on
  demand. See [nightly mutation testing](#nightly-mutation-testing).
- `.github/workflows/codeql.yml` runs CodeQL's `security-and-quality` queries
  on the backend (C#, built with the SDK from `global.json`) and the frontend
  (TypeScript, no build) for changes under `src/`, and weekly. Findings appear
  under the repository's Security → Code scanning alerts. The backend config
  excludes `cs/useless-assignment-to-local`, `cs/useless-upcast` and
  `cs/useless-cast-to-self`, which misread the .NET 11 preview code (about
  1000 false positives, none real). Results in source-generator output under
  `obj/` (Marten/JasperFx evolvers) and in the committed Wolverine code in
  `Internal/Generated` are filtered out of the SARIF with `jq` before it is
  uploaded.
- `.github/workflows/security-scan.yml` checks for known vulnerabilities on
  every backend, frontend, theme or Keycloak image change, and daily (new
  advisories arrive for code that hasn't changed):
  - **NuGet**: `dotnet list package --vulnerable --include-transitive` fails on
    any advisory. A local restore already fails on one too, because NuGet
    audit warnings (NU1901-NU1904) are errors under `TreatWarningsAsErrors`.
    Lift a vulnerable transitive package with a pin in the "Transitive security
    pins" group of `src/backend/Directory.Packages.props`.
  - **npm**: `audit-ci --moderate` in `src/frontend/buddy` and
    `themes/buddy/build`, dev dependencies included. Advisories with no
    patched release are allowlisted in `.github/workflows/security-scan.yml`
    with a reason (currently `GHSA-vfj7-8cjw-p6xm`, braces).
  - **Container images**: builds the API, web and Keycloak images and scans
    them with Trivy. Every finding is uploaded to Security → Code scanning
    (category `trivy-<image>`); the job fails on high or critical ones that
    have a fixed version. Accept one in `.trivyignore` with a reason and an
    `exp:` date.

### Code coverage

The backend and frontend test workflows also measure line and branch coverage.
Each one writes the totals to the job summary, and on a pull request keeps one
comment per side up to date (`.github/scripts/coverage-report.sh`). The full
report is the run's `backend-coverage` or `frontend-coverage` artifact; the
frontend one includes an HTML report. Coverage is reported only, and no
threshold fails a build. Mutation testing is the stronger check of test quality.

To measure locally:

```bash
# Backend: Cobertura XML under ./coverage (coverlet, the API assembly only)
dotnet test src/backend/backend.slnx --settings src/backend/coverage.runsettings --results-directory coverage

# Frontend: src/frontend/buddy/coverage/buddy, open index.html for the HTML report
cd src/frontend/buddy && npx ng test --watch=false --coverage --coverage-reporters=html --coverage-reporters=text-summary
```

### Pre-commit checks

`task hooks:install` (with `AGENT=none` to skip the AI documentation sync)
adds a pre-commit hook. It runs Prettier and ESLint on staged frontend files,
the en/da parity check when translations change, `dotnet format whitespace`
on staged C# files, and Prettier plus `tsc` on staged load-test files. See `.devcontainer/git-hooks/README.md`.

### Nightly mutation testing

The mutation workflow runs every night at 02:00 UTC and can also be started
from **Actions → Mutation testing → Run workflow**, where a `scope` input picks
`frontend` (default), `backend` or `both`.

Scheduled runs can't take inputs, so they read the `MUTATION_SCOPE` repository
variable (**Settings → Secrets and variables → Actions → Variables**) and fall
back to `frontend` when it is unset. The backend job stays opt-in because
Stryker.NET doesn't support .NET 11 yet, so a backend run is expected to fail
for now. Once it's supported, set `MUTATION_SCOPE=both`; no code change is
needed.

A full frontend run doesn't fit in one job, so the job works through the
source files in batches with `scripts/mutation-batch.sh` on all runner vCPUs,
for up to 230 minutes. Then it stops and caches its progress
(`reports/stryker-incremental.json` and `.mutation-batch-progress`), and the
next run carries on from there. A first pass over every file takes a few
nights. After that, a night only re-runs the files whose source or specs
changed. With no cache (first run, or evicted after 7 days unused), the run
starts from the committed `stryker-incremental.json` and goes through every
file again.

With the command test runner, Stryker stores the whole test output of every
mutant run as its `statusReason`, which pushed the report past GitHub's 100 MB
file limit. When the batch script exits, `scripts/mutation-slim-report.mjs`
keeps only the last 1000 characters of each reason, so both the cached and the
committed report stay small. Run it yourself after a direct `npx stryker run`.

Each batch runs only the specs that exercise its files: the colocated
`foo.spec.ts` and every spec that imports `foo`. A file that no spec imports,
such as a child component, uses the specs of the files that import it. The
script passes these specs to `ng test --include` through the `MUTATION_SPECS`
variable in `stryker.conf.json`. This is much cheaper than running the whole
suite for every mutant. The cost is that a mutant only an unrelated spec would
kill is reported as surviving. Without `MUTATION_SPECS`, Stryker runs the whole
suite as before.

Each run writes the mutation score and the number of files covered to the job
summary. It uploads an HTML report of all results so far
(`scripts/mutation-html.mjs`) and the incremental JSON as the
`frontend-mutation-report` artifact.

Runs on the default branch also keep one open issue, **Frontend mutation
testing findings** (label `mutation-testing`), up to date. It lists every
surviving and uncovered mutant as a checklist grouped by file, with a link to
the line, the mutator, and the original and replacement code. Each run rewrites
the issue, so a fixed mutant just drops off the list. The issue is closed once
nothing is left. If the list is longer than GitHub's issue-body limit, the
complete version is in `mutation-findings.md` in the artifact. To generate the
same list locally from your own baseline, run
`node scripts/mutation-issue.mjs` in `src/frontend/buddy`.

Before submitting a documentation-only change, run the relevant Markdown/link
checks and `git diff --check`. Application tests are only necessary when the
change includes executable samples or source code.
