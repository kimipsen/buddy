# TODO

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

- [ ] **`backend-aware-review` depends on a plugin that isn't installed.**
  It routes every backend file through `dotnet-skills:*` skills and agents,
  but only `claude-plugins-official` is configured. Either install the plugin
  and document it in the devcontainer setup, or rewrite the routing to use
  `claude-backend` plus inline checklists, with a fallback when the plugin is
  missing.
- [ ] **`claude-backend` describes a stack the repo doesn't use.** It treats
  EF Core/DbContexts and a hand-rolled Postgres event store as the default,
  while the code uses Marten (event store + inline snapshot projections),
  WolverineFx handlers, FluentValidation and Alba/Testcontainers. There is no
  `DbContext` in `src/backend/buddy`. Rewrite it around the real stack, keep
  the union/Result/UUIDv7/Sonar guidance, and move EF Core into an optional
  reference file.
- [ ] `claude-backend/samples/dotnet-sample/` contains `bin/` and `obj/`
  build output. Delete it and make sure it's ignored.
- [ ] The Sonar false-positive list is duplicated in `claude-backend` and
  `backend-aware-review`. Keep one copy (in `claude-backend` or a shared
  reference file) and link to it from the other.
- [ ] `mutation-fix` only covers the frontend (StrykerJS). Backend mutation
  testing (`task test:mutation:backend`, dotnet-stryker) has no skill (see
  below).

### Missing skills, by priority

1. [ ] **`buddy-frontend`**: the frontend counterpart to `claude-backend`,
   none exists today. It should cover Angular 22 standalone and zoneless
   components with signals, Tailwind 4, the `core/*.service.ts` API layer,
   `postIdempotent` for POSTs, the `features/{child,guardian,…}` layout and
   routes, and the Vitest spec conventions from `docs/testing.md` (the
   `settle()` macrotask flush instead of `whenStable()`, the `matchMedia`
   stub, and specs that stub the service layer). Bring over the useful rules
   from `agents/codex/SKILL.md`. Drop its mandatory PNG wireframe step and
   its duplicated/typo'd sections.
2. [ ] **`backend-feature`**: scaffold a vertical slice under
   `Features/<Domain>/<UseCase>/` (`*.Command.cs`, `*.Handler.cs`,
   `*.Endpoint.cs`), with the validator, authorization helper, `.http` file
   entry, Result → HTTP mapping (`docs/backend/http-status-codes.md`), and
   integration tests in `buddy.IntegrationTests/Features/<Domain>/<UseCase>`.
   New aggregates also need a snapshot projection, a snapshot test and an
   event-shape test (see `docs/backend/analysis/event-stream-snapshots.md`).
3. [ ] **`i18n`** (or a section plus a script in `buddy-frontend`): add
   translation keys to `translations/en` and `translations/da` together,
   and add a parity-check script that fails on missing or orphaned keys.
4. [ ] **`e2e-test`**: write, run and debug Playwright specs. It should list
   the required services (Postgres/Keycloak/Mailpit), explain the
   direct-grant `e2e/support/auth-fixture.ts` (and when to use the real login
   form like `login.spec.ts` does), point to the report
   (`task test:e2e:frontend:report`), and describe how to triage flaky
   versus real failures.
5. [ ] **`run-buddy`**: a project launch skill. The built-in `run` skill
   checks for a project skill first. It should cover starting the API
   (5193/7076) and the Angular dev server (4300), checking
   Keycloak/Postgres/Mailpit health, runtime config, and using the
   `db:marten:*` tasks to inspect events and streams.
6. [ ] **`mutation-fix-backend`**, or a backend mode for `mutation-fix`: the
   same loop (run, triage survivors, strengthen integration tests, re-run)
   for dotnet-stryker, including how to scope a run to changed files, since
   a full run is slow.
7. [ ] **`feature-from-analysis`**: full-stack orchestration that takes a
   `docs/*/analysis/*.md` design, plans it, builds the backend slice, the
   frontend service, components, i18n and the e2e spec, then ticks the
   README feature checkbox. The Sleep diary (proposed, not implemented) is
   the natural first run. Also covers writing new analysis docs in the
   house style.
8. [ ] **`sonar-triage`** (lower priority): pull the SonarCloud findings,
   classify them against the known false positives, and fix only the real
   ones.
9. [ ] **`deploy`** (lower priority, always confirm first): check the
   compose or Azure deploy prerequisites (`deploy/.env`,
   `deploy/azure/.env`), run the right task, and verify it afterwards.

### Supporting housekeeping

- [ ] Add a root `CLAUDE.md` (run `/init`) with the stack, task commands,
  ports and conventions, so skills can link to it instead of repeating it.
- [ ] Decide whether `agents/` or `.claude/skills/` is the canonical home.
  Either generate Claude skills from `agents/` or mark `agents/` as
  Codex/Copilot-only in `agents/README.md`.
- [ ] Prune stale `.claude/worktrees/agent-*` checkouts (they hold old
  copies of the skills and TODO).
- [ ] After each new skill, test that it triggers using `skill-creator`'s
  eval and description-tuning workflow.

### Suggested order

Fix `backend-aware-review`'s missing dependency first, because it's broken
today. Then rewrite `claude-backend` for Marten/Wolverine and add
`CLAUDE.md`. Then build `buddy-frontend` (with i18n) and `backend-feature`,
since they cover most day-to-day work. After that, add `e2e-test` and
`run-buddy`, then `feature-from-analysis`, using Sleep diary as its first
real test. `mutation-fix-backend`, `sonar-triage` and `deploy` can come
whenever they're needed.
