# TODO

## Print functionality

- [x] Include subtasks in printed output and add a toggle to turn their
  inclusion on/off. Printing currently ignores subtasks and offers no toggle.

## Pickup/drop-off options

- [x] Expand the pickup/drop-off options to include nanny/babysitter.

## Consistent list sorting

- [ ] Make sure lists are sorted the same way everywhere. Names should use one
  locale-aware order (`sortByName` / `sortByChildName` in `core/array-utils.ts`),
  and the default selection should be the first item in that order, not the
  first one the API returned. Today the order is mixed:
  - Sorted by full name: manage-children, pickup/drop-off (fixed 2026-10-05).
  - Sorted by "given family" with their own comparator: manage-groups members
    (`sortByMemberName`), pickup-today (`childName`, with an id tiebreak).
  - API order (`ListForGuardianAsync`, ordered by link `CreatedAt`):
    task library, progress goals, sleep diary, medicines, meal plan
    assign/today/AI assistant, doses today, children overview, agenda, print
    editor and week plan, manage-groups' "add child" list.
  - Decide whether to sort in the backend instead, so every client gets one order.

## Invitation documentation

- [ ] Update the documentation to state that invitations can only be accepted
  by accounts with a verified email address.

## Group deletion

- [x] Add functionality to delete a group.

## Guardian onboarding

- [ ] Implement the [guardian onboarding guide](docs/frontend/analysis/guardian-onboarding.md)
  for users logging in with no groups and no linked children: create one group,
  add one or more children, optionally invite other guardians/parents, create
  a shared calendar, schedule a task with multiple subtasks, and set up a meal
  plan. Include resumable progress and confirm the plan's open questions first.

## Shared color picker

- [x] Create a common color picker and use it everywhere a color can be
  set. Standardize on a fixed palette of selectable colored circles,
  replacing the existing color square/wheel and other inconsistent
  color-selection controls.

## Event-stream snapshots rollout — done

All 14 event-sourced aggregates now have an inline Marten snapshot
projection in the shared `snapshots` schema — see
[docs/backend/analysis/event-stream-snapshots.md](docs/backend/analysis/event-stream-snapshots.md)
for the design and every gotcha hit along the way (naming collisions with
Marten's source generator, `Register()` vs `Snapshot<T>()`, class-vs-struct
document identity, strongly-typed-id and `ValueTuple` dictionary keys over
JSON, and a discriminated-union JSON-shape collision found in `Calendars`).
Full suite: 438/438 passing.

"Cut over" means the aggregate's read-only query handler now calls
`FindSnapshotAsync` instead of `ReadAsync` + `Rehydrate`; a few aggregates
have no single-entity "get by id" read path at all (only list/write
handlers touch them), so there was nothing to cut over — `FindSnapshotAsync`
is still there on the store for future use.

- [x] Groups — `Group` (cut over: `GetGroup`)
- [x] Calendars — `Calendar` (cut over: `GetCalendar`)
- [x] Calendars — `CalendarItem` (no single-get handler exists; not cut over)
- [x] Guardians — `GuardianLink` (no single-get handler exists; not cut over)
- [x] Mealplans — `Meal` (no single-get handler exists; not cut over)
- [x] Mealplans — `MealPlan` (cut over: `GetSharedGroup`)
- [x] Mealplans/AiAssistant — `AiProviderCredential` (cut over: `ListProviders`)
- [x] Mealplans/AiAssistant — `MealplanAiSession` (cut over: `GetCurrentAiSession`)
- [x] Medicines — `MedicineSchedule` (no single-get handler exists; not cut over)
- [x] Medicines — `MedicineSharing` (cut over: `GetSharedMedicineGroup`)
- [x] Pickups — `PickupSchedule` (cut over: `PickupScheduleExpansion`/`ListPickupSchedule`)
- [x] Progress — `ChildProgress` (cut over: `GetMyProgress`, `GetChildProgress`)
- [x] TaskLibrary — `TaskTemplate` (cut over: `ListTaskTemplates`)
- [x] Users — `User` (cut over: `GetCurrentUser`)

Not aggregates (event streams with no rehydrated state, so nothing to
snapshot): `GuardianInvite` events drive `GuardianInviteDocument` only, same
as `GroupInvite*` events drive `GroupInviteDocument` off the `Group` stream.

## Follow-ups worth doing, not done here

- The aggregates without a single-get handler (`CalendarItem`, `GuardianLink`,
  `Meal`, `MedicineSchedule`) only have `FindSnapshotAsync` sitting unused on
  their store. Either a future read path will use it, or it's dead code
  worth reconsidering.
- No backfill/rebuild tooling is wired up yet (see the design doc's
  "Backfilling existing streams" section) — fine for now since this
  environment has no pre-existing production streams predating the
  snapshot projections, but needed before any real deployment with existing
  data.
- `ValueTupleJsonConverterFactory` only implements arity 2 and 3 (the
  shapes actually used today); a new tuple shape needs a new `ConverterN`
  added to it.

## Claude skills — gap review (2026-09-30)

Current project skills (`.claude/skills/`): `claude-backend`,
`backend-aware-review`, `mutation-fix`. The `agents/` packages (codex,
copilot, github-pilot) are not Claude Code skills (no frontmatter, not under
`.claude/skills/`), so Claude never loads them automatically.

### Problems in the existing skills

- [x] **`backend-aware-review` depends on a plugin that isn't installed.**
  It routes every backend file through `dotnet-skills:*` skills and agents,
  but only `claude-plugins-official` is configured. Either install the plugin
  and document it in the devcontainer setup, or rewrite the routing to use
  `claude-backend` plus inline checklists, with a fallback when the plugin is
  missing.
- [x] **`claude-backend` describes a stack the repo doesn't use.** It treats
  EF Core/DbContexts and a hand-rolled Postgres event store as the default,
  while the code uses Marten (event store + inline snapshot projections),
  WolverineFx handlers, FluentValidation and Alba/Testcontainers. There is no
  `DbContext` in `src/backend/buddy`. Rewrite it around the real stack, keep
  the union/Result/UUIDv7/Sonar guidance, and move EF Core into an optional
  reference file.
- [x] `claude-backend/samples/dotnet-sample/` contains `bin/` and `obj/`
  build output. Delete it and make sure it's ignored.
- [x] The Sonar false-positive list is duplicated in `claude-backend` and
  `backend-aware-review`. Keep one copy (in `claude-backend` or a shared
  reference file) and link to it from the other.
- [x] `mutation-fix` only covers the frontend (StrykerJS). Backend mutation
  testing (`task test:mutation:backend`, dotnet-stryker) has no skill (see
  below).

### Missing skills, by priority

1. [x] **`buddy-frontend`**: the frontend counterpart to `claude-backend`,
   none exists today. It should cover Angular 22 standalone and zoneless
   components with signals, Tailwind 4, the `core/*.service.ts` API layer,
   `postIdempotent` for POSTs, the `features/{child,guardian,…}` layout and
   routes, and the Vitest spec conventions from `docs/testing.md` (the
   `settle()` macrotask flush instead of `whenStable()`, the `matchMedia`
   stub, and specs that stub the service layer). Bring over the useful rules
   from `agents/codex/SKILL.md`. Drop its mandatory PNG wireframe step and
   its duplicated/typo'd sections.
2. [x] **`backend-feature`**: scaffold a vertical slice under
   `Features/<Domain>/<UseCase>/` (`*.Command.cs`, `*.Handler.cs`,
   `*.Endpoint.cs`), with the validator, authorization helper, `.http` file
   entry, Result → HTTP mapping (`docs/backend/http-status-codes.md`), and
   integration tests in `buddy.IntegrationTests/Features/<Domain>/<UseCase>`.
   New aggregates also need a snapshot projection, a snapshot test and an
   event-shape test (see `docs/backend/analysis/event-stream-snapshots.md`).
3. [x] **`i18n`** (or a section plus a script in `buddy-frontend`): add
   translation keys to `translations/en` and `translations/da` together,
   and add a parity-check script that fails on missing or orphaned keys.
4. [x] **`e2e-test`**: write, run and debug Playwright specs. It should list
   the required services (Postgres/Keycloak/Mailpit), explain the
   direct-grant `e2e/support/auth-fixture.ts` (and when to use the real login
   form like `login.spec.ts` does), point to the report
   (`task test:e2e:frontend:report`), and describe how to triage flaky
   versus real failures.
5. [x] **`run-buddy`**: a project launch skill. The built-in `run` skill
   checks for a project skill first. It should cover starting the API
   (5193/7076) and the Angular dev server (4300), checking
   Keycloak/Postgres/Mailpit health, runtime config, and using the
   `db:marten:*` tasks to inspect events and streams.
6. [x] **`mutation-fix-backend`**, or a backend mode for `mutation-fix`: the
   same loop (run, triage survivors, strengthen integration tests, re-run)
   for dotnet-stryker, including how to scope a run to changed files, since
   a full run is slow.
7. [x] **`feature-from-analysis`**: full-stack orchestration that takes a
   `docs/*/analysis/*.md` design, plans it, builds the backend slice, the
   frontend service, components, i18n and the e2e spec, then ticks the
   README feature checkbox. The Sleep diary (proposed, not implemented) is
   the natural first run. Also covers writing new analysis docs in the
   house style.
8. [x] **`sonar-triage`** (lower priority): pull the SonarCloud findings,
   classify them against the known false positives, and fix only the real
   ones.
9. [x] **`deploy`** (lower priority, always confirm first): check the
   compose or Azure deploy prerequisites (`deploy/.env`,
   `deploy/azure/.env`), run the right task, and verify it afterwards.

### Supporting housekeeping

- [x] Add a root `CLAUDE.md` (run `/init`) with the stack, task commands,
  ports and conventions, so skills can link to it instead of repeating it.
- [x] Decide whether `agents/` or `.claude/skills/` is the canonical home.
  Either generate Claude skills from `agents/` or mark `agents/` as
  Codex/Copilot-only in `agents/README.md`.
- [x] Prune stale `.claude/worktrees/agent-*` checkouts (they hold old
  copies of the skills and TODO). `.claude/worktrees/` no longer exists.
- [ ] After each new skill, test that it triggers using `skill-creator`'s
  eval and description-tuning workflow.

### Status (2026-10-01)

All of the skills above are written, plus `CLAUDE.md`. `backend-aware-review`
now uses `dotnet-skills` only when the plugin is installed and falls back to
an inline checklist otherwise. Still open:

- [x] Worktree `.claude/worktrees/agent-a1f407bec71b27f87` is merged but has
  an uncommitted one-line `Program.cs` change. Keep or drop it, then
  `git worktree remove` it. (`agent-a73bc152…` is already removed.)
- [x] Test that the new skills trigger, using `skill-creator` evals.

### Issues the skill agents found in the codebase

Backend:
- [x] Marten `AppendAsync` never passes an expected version, so concurrent
  read-modify-append on the same stream is last-writer-wins.
- [x] Backend mutation testing is blocked: Stryker.NET 4.16 can't discover
  tests on the .NET 11 preview SDK, and it still exits 0 when that happens.
- [x] 30 event types have no golden-file event-shape test (all of Progress,
  AiAssistant, sharing/iCal/invite/policy events). Nothing enforces coverage.
- [x] `EventShapeTestSupport` doesn't register `ValueTupleJsonConverterFactory`,
  though some stores do.
- [x] `http-status-codes.md` says creates return 201; every endpoint returns 200.
- [x] There are two resend-cooldown implementations (409 in
  `ResendEmailVerification`, 400 in the invites). Unify them.
- [x] Stale comments refer to `AssignPickupHandler.ValidateFields`
  (`ValidatorExtensions.cs`, `PickupAssignment.cs`).
- [x] `event-stream-snapshots.md` intro still says there is no snapshotting.

Frontend:
- [x] Extra keys in the Danish dictionary aren't caught by the build
  (`docs/frontend/README.md` says they are). Run `check-parity.mjs` in CI.
- [x] There are 7 unused translation keys
  (`node .claude/skills/i18n/check-parity.mjs --unused`).
- [x] There's no linter, and 275 files don't pass `prettier --check`.
- [x] Doc errors: the frontend README mentions zone.js wiring, and the root
  README undersells `src/app/shared/`.

Environment, CI and deploy:
- [x] The e2e workflow triggers on push to `main`, but the branch is `master`.
- [x] Postgres hits its 100-connection limit. bob has about 50 leftover e2e
  children, and the dashboard makes one request per child.
- [x] A nested devcontainer stack on `localhost:9080`/`9025` has a different
  Keycloak, whose tokens fail on the API.
- [x] `.devcontainer/README.md` names `init-keycloak-db.sql`; the file is
  `.sh`.
- [x] Deploy:
  - No rollback is documented.
  - `task deploy` deploys to whatever Docker host it runs on.
  - The backup command uses the wrong volume name.
  - Compose Keycloak runs `start --optimized` with no Postgres build step.
  - The Azure docs number their steps inconsistently.

### Fix round (2026-10-01)

Everything above is fixed except the items still unticked. Highlights:
- **Concurrency:** writes now use expected-version appends, handled
  centrally, and a conflict returns `409 concurrency_conflict`.
- **Cooldown:** all three resend endpoints now return 409 `resend_cooldown`.
- **Golden files:** every event type has one, enforced by
  `Meta/EventGoldenFileCoverageTests`.
- **Connection pool:** the nine Marten stores share one `NpgsqlDataSource`,
  capped at 50 (`Common/Postgres/PostgresDataSource.cs`).
- **Request fan-outs:** per-child, per-group and per-calendar requests are
  capped at 4 in flight (`core/map-with-concurrency.ts`).
- **Stryker:** upgraded to Stryker.NET 5.0.0 and run through
  `run-stryker.sh`, which swaps in the SDK's Roslyn. It now tests mutants,
  and a log guard makes silent failures fail the task and the workflow.
- **i18n:** a missing or extra Danish key fails the type check, and the
  parity check runs in the new `frontend-tests.yml`.
- **Deploy:**
  - `preflight.sh` guard
  - SHA-tagged images
  - healthchecks
  - `pg_dump` backups
  - Keycloak built for Postgres
  - rollback docs
- **e2e:**
  - tests now clean up the data they create
  - a script removes leftover test data
  - fixed the AI-assistant spec's timing bug
  - fixed the `loginAs` redirect race

Still open:
- [x] ESLint plus a one-time prettier reformat of 275 files, as a
  formatting-only commit after these fixes are committed.
- [x] Two e2e specs failed only in a parallel full run. Parallel specs were
  sharing seeded guardians: `email-verification` kept changing bob's email,
  and pages used another test's child that had just been unlinked. Specs
  that create children or touch family state now use a throwaway
  `newGuardian()`. The full suite passed 20/20 five times in a row.
- [x] Family-scope pages default to `children[0]` of an unordered list
  (`ListForGuardianAsync` has no ORDER BY). Add a stable order.
- [x] Stop the nested `devcontainer-*` containers, and remove
  worktrees `agent-a1f407bec71b27f87` (its change is already on master) and
  `agent-a13f8a94103b2fea2` (merged into the working tree by hand). Auto mode
  blocked this; it needs you.
- [ ] **Before the next VM deploy:** check whether the VM's Keycloak realm
  lives in H2 inside the container (see `deploy/README.md`, "Upgrading from
  the stock Keycloak image") and export it first. Also add `DEPLOY_HOST` to
  the VM's `deploy/.env`.
- [x] **Decided: resolve the key through any linked child.** The family AI credential is attached to
  one child, so unlinking that child silently drops the family's key. When
  families merge, `ResolveFamilyAiCredentialIdAsync` picks a credential out
  of a `HashSet`, so the choice is effectively random. Move the credential
  to the guardian or family, or resolve it through any linked child.
- [x] Backend mutation survivors: `CreateChild.Validator` FamilyName and
  Username rules aren't covered by tests (found by the first real Stryker
  run).
- [ ] Stryker Safe Mode drops about 1277 of 4734 mutants as `CompileError`
  (CS0165), so some methods are never mutation-tested.

### Round 3 (2026-10-01)

- Validator audit: every rule in the ~40 validators now has a test that
  fails if the rule is removed, and the test asserts which field failed. The
  CreateChild validator scores 100% under Stryker.
- Fixed: `PUT /mealplans/groups/{groupId}/plan` never validated its input
  and accepted notes of any length. It now has the same 2000-character limit
  as the family endpoint.
- AI credential: the lookup goes through any child the guardian is or was
  linked to (revoked links only count if the guardian contributed to the
  key). It picks the most recently activated key, and the rule is documented
  in `docs/backend/mealplans/flow.md`.
- Skill triggers: tested with 158 real model runs. Four descriptions were
  tightened, and all 120 eval prompts now route to the right skill.
- Lint: `cbbc0e3` adds ESLint, `af9b173` is the prettier reformat, and
  `.git-blame-ignore-revs` makes blame skip the reformat.

Still open:
- [ ] **Before the next VM deploy:** check whether the VM's Keycloak realm
  lives in H2, and add `DEPLOY_HOST` to the VM's `deploy/.env` (see above).
- [x] Six `interactive-supports-focus` lint warnings on click-to-dismiss
  backdrops need an accessibility pass.
- [x] `CreateCalendar`'s `Icon.Value` NotEmpty rule is dead code, because
  the endpoint turns a blank icon into null.
- [ ] Stryker Safe Mode drops about 1277 mutants as `CompileError`
  (a Stryker limitation).

### Suggested order

Fix `backend-aware-review`'s missing dependency first, because it's broken
today. Then rewrite `claude-backend` for Marten/Wolverine and add
`CLAUDE.md`. Then build `buddy-frontend` (with i18n) and `backend-feature`,
since they cover most day-to-day work. After that, add `e2e-test` and
`run-buddy`, then `feature-from-analysis`, using Sleep diary as its first
real test. `mutation-fix-backend`, `sonar-triage` and `deploy` can come
whenever they're needed.

## Week plan printing and work locations (2026-10-02)

Built and staged: guardian work locations, the print templates backend and the print frontend.
The design and every decision are in
[docs/frontend/analysis/week-plan-printing.md](docs/frontend/analysis/week-plan-printing.md),
[docs/backend/analysis/week-plan-print-templates.md](docs/backend/analysis/week-plan-print-templates.md)
and [docs/backend/analysis/work-locations.md](docs/backend/analysis/work-locations.md).

Before calling it done:
- [ ] Commit the staged print-templates backend and print frontend.
- [ ] Have a Danish speaker review `translations/da/print.ts` and `translations/da/work-locations.ts`.
  Claude wrote both; check in particular "Forælder" as the label for a guardian.
- [ ] Print from Safari and check that A3/A4 landscape comes from `@page`. If Safari ignores it
  and you have to pick the paper in the print dialog, write that down or consider a
  server-side PDF.
- [ ] Print from the installed web app on the iPad, through the share sheet.
- [ ] Print a real week on paper and compare it with the old fridge sheet.

Found along the way:
- [ ] Some e2e specs fail only in a parallel full run: calendar, groups, task library and AI
  settings. They pass when run serially. They are unrelated to printing, but a cold dev server
  with several workers seems to push them over the timeout.
- [ ] The `db:marten:*` tasks and `MARTEN_SCHEMAS` live only in the git-ignored `taskfile.yml`,
  not in `taskfile.dist.yml`. `worklocations` and `printtemplates` were added locally, but other
  clones don't get them. The skills and `CLAUDE.md` still point at `taskfile.yml`.
  Either move the tasks into `taskfile.dist.yml` or document the local setup.

Deferred until someone asks (each is additive; details are in the docs' open questions):
- [ ] Print several weeks at once, for example a month as one PDF.
- [ ] A `TransferPrintTemplateToGroup` slice, for turning a personal template into a shared one.
- [ ] A configurable span, for example 14 days, if it stays legible on A4.
- [ ] More row kinds, for example medicine doses and goal-post progress.
- [ ] Work locations:
  - A strict ISO-parity cycle mode, for patterns written as "even weeks".
  - Effective-from dates for pattern changes, so a change doesn't rewrite past days.
  - A view tier for group members who aren't co-guardians.
  - Restoring an archived location.

## Bugs found while generating the docs screenshots (2026-10-03)

Found while seeding the demo family for `task docs:screenshots`.

- [x] **Completed tasks stayed "Overdue" on the guardian dashboard.** `isOverdue` in
  `features/guardian/tasks-today/tasks-today.ts` only checked `dueAt < now`. A finished task (a
  plain task, or a routine with every subtask done) is now never overdue, however late it was
  finished. The Overdue and Due today sections are computed from the current completion state,
  so ticking an overdue task off moves it to Due today straight away, and unticking it moves it
  back.
- [x] **Long pickup labels crossed the diagonal on the printed week plan.** A playdate label
  ("Playdate: Oscar's family") wrapped across the diagonal line and out of its half of the cell.
  Both labels are now held to 70% of the cell width and at most two lines. The pickup label is
  right-aligned in its corner, and labels over 10 characters print at 0.9em instead of 1.2em
  (`week-plan-sheet.ts`).

Found along the way:
- [ ] The full frontend unit suite (`npm test -- --watch=false`) times out in a different set of
  specs on each run when the machine is busy (load average 20–30 on 16 cores, with dev servers,
  language servers and a second agent session running). Every spec passes when run in smaller
  groups (`--include 'src/app/core/**/*.spec.ts'` and so on). The specs wait with `settle()`
  loops and real timers. Consider limiting the Vitest worker count, or raising the per-test
  timeout for the unit-test builder.

## Best-practice gap review (2026-10-06)

### Security and privacy

- [x] **Containers run as root.** Both Dockerfiles now switch to their `aspnet`/`caddy` user.
  Caddy keeps :80 (the base image gives its binary `cap_net_bind_service`) and owns `/data` and
  `/config`; `aspnet` has a home directory for the Data Protection key ring.
- [ ] **Data Protection keys aren't persisted.** `AddDataProtection()` in `MealplansFeature`
  stores its key ring in the container's home directory, so every redeploy (new container) loses
  it and stored AI-provider API keys can no longer be decrypted. Persist the keys (Postgres via a
  Marten-backed `IXmlRepository`, or a volume) and protect them at rest.
- [x] **Security headers.** The app's headers are in `src/frontend/buddy/Caddyfile` (so they also
  apply on Azure); the API's are in `deploy/Caddyfile`. Keycloak sends its own.
- [ ] **Enforce the app's Content-Security-Policy.** It is report-only for now. Two things break
  under enforcement: the inline theme script in `index.html` (move it to a file, or add its hash)
  and the `onload` handler Angular's critical-CSS inlining puts on the stylesheet `<link>`
  (disable `inlineCritical`, or allow it with `'unsafe-hashes'`). Then switch the header to
  `Content-Security-Policy`. The API on Azure has no edge proxy, so it gets no security headers
  there yet.
- [ ] **GDPR for special-category health data** -- design and progress in
  `docs/backend/analysis/gdpr-data-protection.md` (implementation order there):
  - [x] Quick fixes: deleted users locked out, Keycloak account deleted, tokens redacted from
    telemetry, idempotency responses encrypted.
  - [x] Right to erasure in the event store: per-feature erasers (delete or mask), the
    `DELETE /users/me` cascade, `UserErasureService`, the erasure ledger.
  - [x] `DeleteChild` endpoint (sole guardian only) and the account-deletion preview, plus the
    frontend: a delete-child action and a deletion dialog that lists the children and groups
    affected.
  - [x] Data export (`GET /users/me/export`) and a "Download my data" button.
  - [ ] AI assistant: minimization, 30-day retention, disclosure and acknowledgement.
  - [ ] Health-data read audit logs.
  - [ ] Outside the code: privacy notice, record of processing, DPIA, DPAs with the hosting
    provider; set up 30-day backup rotation.
- [ ] **Security scanning in CI.** Add CodeQL, an `npm audit` / `dotnet list package --vulnerable`
  gate and a container image scan (e.g. Trivy).
- [x] **Least-privilege `permissions:`** (`contents: read`) in every workflow.
- [ ] **Pin GitHub Actions by commit SHA** instead of tag (Dependabot keeps SHA pins updated).

### Operability

- [x] **Observability.** OpenTelemetry traces, metrics and logs (ASP.NET Core, HttpClient, Npgsql,
  Wolverine, runtime), exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; JSON console
  logs with TraceId/RequestId outside Development. See `docs/backend/observability.md`.
- [ ] **Run an OTLP backend in production** (Collector, Grafana, Azure Monitor...) and set
  `OTEL_EXPORTER_OTLP_ENDPOINT`; until then nothing is exported. Consider an Aspire dashboard
  service in the devcontainer for local traces.
- [x] **More logging.** Audit-style logs for account and access changes, plus logs for swallowed
  and infrastructure failures; IDs only (docs/backend/observability.md, `AuditLogTests`).
- [x] **Global exception handler.** `UnhandledExceptionHandler` turns any unhandled exception into a
  500 `internal_error` (or 503 `dependency_unavailable` with `Retry-After` when Postgres, Keycloak or
  SMTP can't be reached) `ErrorEnvelope`, logged with the matching requestId.
- [x] **Readiness health checks.** `/health/ready` checks Postgres (503) and Keycloak (degraded,
  still 200); the prod compose file probes it for `api` and `/` for `frontend`.
- [ ] **Health probes on Azure Container Apps.** `deploy.sh` configures none; use `/health` as the
  liveness and `/health/ready` as the readiness probe.
- [ ] **Automated backups.** Backups on the Oracle VM are manual `pg_dump` commands
  (`deploy/README.md` §7). Schedule them, copy them off the VM, and test a restore regularly.
- [x] **Pin the base images.** Official `sdk:11.0.100-preview.7` / `aspnet:11.0.0-preview.7`
  (matching `global.json`) instead of floating nightlies; every base and compose image pinned by
  digest, refreshed by Dependabot.

### Build reproducibility

- [x] **Pin the .NET SDK.** `global.json` at the repo root pins the devcontainer's SDK, and the
  workflows install it via `global-json-file` (CI was on rc.1 while the devcontainer ran preview.7).
- [x] **Pin the Node version.** `.nvmrc` (24) read by CI and matched by the Dockerfile,
  `engines` + `engine-strict`, and Dependabot no longer bumps the Node major.
- [x] **Drop `--legacy-peer-deps`.** It hid no conflicts; `npm ci` resolves cleanly without it.
  Both Docker build contexts now have a `.dockerignore`, so local `node_modules`/`bin`/`obj` can't
  leak into an image.
- [x] **Enable .NET analyzers.** `latest-recommended` plus code style enforced on build, findings
  fixed; deliberate exceptions are in `src/backend/.editorconfig`.
- [ ] **`npm audit` reports 14 vulnerabilities (3 critical)** in the frontend dependencies; triage
  them (see the security-scanning item).

### Process

- [x] **Code coverage in CI** for backend and frontend: job summary, a PR comment per side, and
  the full report as an artifact (backend 89% lines / 70% branches, frontend 97% / 92%).
- [x] **Repo hygiene files.** MIT `LICENSE`, `SECURITY.md` (GitHub private reporting),
  `.github/CODEOWNERS` and a PR template.
- [x] **Enable private vulnerability reporting** on `kimipsen/buddy` (the primary repo);
  `SECURITY.md` points reporters there.
- [x] **Pre-commit lint/format hook** in `.devcontainer/git-hooks` (installed by
  `task hooks:install`): Prettier, ESLint, i18n parity and C# whitespace on staged files.
