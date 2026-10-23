# TODO

<!-- A ticked item moves to the bottom of Done under its own section heading (CLAUDE.md), so the
     same heading appears more than once there. -->
<!-- markdownlint-disable MD024 -->

## Open

### Publishing Docker images (2026-10-19)

Plan in [Publishing Docker images on GitHub](docs/backend/analysis/publish-docker-images.md).

- [ ] **First deploy from the published images.** Run `task deploy:pull` on the arm64 VM (from
      the release tag) and `deploy.sh` with `BUDDY_IMAGE_VERSION` on Azure once each, and check
      step 6/7 of the deploy guides. Only tried with a stubbed `docker` so far.
- [ ] **Retention.** Clean up old `sha-*` and orphaned untagged versions with a tool that keeps
      the platform images of a multi-arch image (not `actions/delete-package-versions`).

### Backend unit tests as the primary test source (2026-10-16)

Make backend unit tests the primary test source, with integration tests on demand. Plan in
[Backend unit tests](docs/backend/analysis/backend-unit-tests.md).

- [ ] **Phase 1: split the projects.** Add `buddy.UnitTests` (no Docker), move the ~25 pure test
      files and golden files, make `task test:backend` unit-only and add
      `task test:backend:integration`; update CI and CLAUDE.md.
- [ ] **Phase 2: test infrastructure.** In-memory fakes for the 27 event stores, store contract
      tests against fakes and Marten, event builders, recording fakes for Keycloak, mail and AI.
- [ ] **Phase 3: validators and pure domain.** The 93 validators, aggregate `Rehydrate`, and the
      authorization helpers.
- [ ] **Phase 4: handlers, domain by domain.** One test class per handler for
      each of the 19 feature folders; trim duplicated integration tests.
- [ ] **Phase 5: tooling.** Run Stryker against the unit project; update `backend-feature`,
      `claude-backend`, `mutation-fix-backend` and `feature-from-analysis`.

### Tools for families with ADHD and autism (2026-10-11)

Ideas for new features beyond what Buddy already has. The four marked **(start here)** and the
homework splitter have shipped; they are listed under Done.

#### In-the-moment help for the child

- [ ] **Now / Next / Later strip.** A reduced day view with three cards, for overwhelmed moments.
      See [Now / Next / Later strip](docs/frontend/analysis/now-next-later.md).
- [ ] **Pictures on meals, and photos.** The pictogram picker on meals (needs food pictograms)
      and guardian-uploaded photos (needs a shared file-storage design). See the open questions in
      [Pictogram support](docs/frontend/analysis/pictograms.md).

#### Regulation and emotions

- [ ] **Calm-down toolbox.** Strategies the guardian and child pick together, reachable in one tap
      from the day view, with a guided breathing animation.
- [ ] **Sensory profile.** Per-child notes on sounds, textures, foods and situations to avoid or
      that help, shareable with babysitters and grandparents.

#### Insight for guardians and clinicians

- [ ] **Behaviour (ABC) log.** Antecedent, behaviour and consequence entries, correlated with
      sleep, doses and schedule changes to surface patterns.

#### School and other caregivers

- [ ] **Home–school notebook.** A short daily message thread between home and school; the teacher
      reads and replies through a link, without an account.
- [ ] **Caregiver handover card.** Routine, medicine, house rules, calm-down tips and contacts for
      one evening on a single share link, building on saved babysitters.

#### Smaller ideas

- [ ] **Social stories.** Short illustrated stories that prepare the child for new events, such as
      the dentist or a trip.
- [ ] **Guardian well-being check-in.** A short private note for guardians themselves.

### Menu and settings cleanup

- [ ] Decide how and where things are configured, so settings live in one
      predictable place instead of being spread across pages.

### Mobile app

- [ ] Build a mobile app for Buddy. Decide the approach first (installable PWA,
      a wrapper such as Capacitor around the Angular app, or native) and cover login
      through Keycloak, push notifications and app-store distribution. Design:
      [Buddy as a mobile app](docs/frontend/analysis/mobile-app.md) (phases, sign-in,
      stores) and
      [Mobile app and push notifications](docs/backend/analysis/mobile-app-and-notifications.md)
      (push).

### Security and privacy (best-practice gap review, 2026-10-06)

- [ ] **Encrypt the Data Protection keys at rest.** They are stored unencrypted in the
      `dataprotection` schema, so anyone who can read the database (or a `pg_dump` backup) can decrypt
      stored API keys. Add `ProtectKeysWithCertificate` with a certificate from the secret store.
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
  - [x] AI assistant: minimization, 30-day retention, disclosure and acknowledgement.
  - [x] Health-data read audit logs.
  - [ ] Outside the code: privacy notice, record of processing, DPIA, DPAs with the hosting
        provider; set up 30-day backup rotation. Step-by-step guide: `docs/privacy-setup.md`.

### Week plan printing (2026-10-02)

Deferred until someone asks (each is additive; details are in the docs' open questions):

- [ ] More row kinds, for example medicine doses and goal-post progress.

### Mutation testing (Claude skills gap review, 2026-10-01)

- [ ] Stryker Safe Mode drops about 1277 of 4734 mutants as `CompileError`
      (CS0165), so some methods are never mutation-tested.
      (2026-10-08) Still open: 5.0.0 is the latest Stryker.NET, so there is no upgrade to try.

## Done

### Print functionality

- [x] Include subtasks in printed output and add a toggle to turn their
      inclusion on/off. Printing currently ignores subtasks and offers no toggle.
- [x] Add an option to include a watermark QR code on the print preview. The
      QR code should take whoever scans it to the web application's login page.
- [x] Add a "Tasks and appointments" row kind (`CalendarAgenda`) that prints a
      calendar's tasks and appointments together: tasks with a tick box,
      appointments with a clock and their start time.

### Pickup/drop-off options

- [x] Expand the pickup/drop-off options to include nanny/babysitter.

### Consistent list sorting

- [x] Make sure lists are sorted the same way everywhere. The core services now
      sort what they return (`sortByName` / `sortByFullName` / `sortByPersonName` in
      `core/array-utils.ts`), so every page, and its default selection, gets the same
      locale-aware name order: children, guardians, siblings, groups and their
      members, calendars and assignable members, babysitters, medicines, meals,
      task templates and print templates (2026-10-07).

### Invitation documentation

- [x] Update the documentation to state that invitations can only be accepted
      by accounts with a verified email address (2026-10-09).

### Group deletion

- [x] Add functionality to delete a group.

### Guardian onboarding

- [x] Implement the [guardian onboarding guide](docs/frontend/analysis/guardian-onboarding.md)
      for users logging in with no groups and no linked children: create one group,
      add one or more children, optionally invite other guardians/parents, create
      a shared calendar, schedule a task with multiple subtasks, and set up a meal
      plan. Include resumable progress. Open questions settled 2026-10-08 (see the doc's
      "Decisions made"); step 1, the child-password reset, is done (2026-10-08). The guide
      shipped 2026-10-08.

### In-app help

- [x] Add in-system help so users can learn how each page and feature works
      without leaving the app. Consider building it alongside, or reusing parts of,
      the guardian onboarding guide above (for example, step explanations or a way
      to replay the guide on demand). Write a design doc in `docs/frontend/analysis/`
      first.

### Shared color picker

- [x] Create a common color picker and use it everywhere a color can be
      set. Standardize on a fixed palette of selectable colored circles,
      replacing the existing color square/wheel and other inconsistent
      color-selection controls.

### Remove personal information from the docs

- [x] Remove my name and other personal details from the documentation, for
      example the `# Kim Documentation` heading in `docs/README.md`. `git grep -i -E
'\bkim\b|ipsen'` finds about 30 more files to check (skills, `.http` files,
      specs, `.vscode/settings.json`, the Stryker report). Keep the ones that have to
      name the owner (`LICENSE`, `.github/CODEOWNERS`, the `kimipsen/buddy` repo links).

### Feature flags

- [x] Add feature flags so each Buddy installation can turn features on or off
      (for example the AI meal assistant, medicines or printing). Decide where flags
      live (configuration per installation vs. an admin setting), how the backend
      enforces them (endpoints and handlers) and how the frontend hides routes and
      navigation for disabled features. Write a design doc in `docs/backend/analysis/`
      first.

### Responsive design

- [x] Make the layout adapt when the window changes between screen sizes
      (phone, tablet, desktop), for example when resizing the browser or rotating a
      tablet, not only on first load. Check every page, and extend the docs
      screenshots with a tablet size if needed.

### Keep range length when the start changes

- [x] In every form with a start and an end (for example calendar items), move
      the end along with the start so the length stays the same. If an item runs
      from 9:00 to 9:30 and the start is changed to 10:00, the end becomes 10:30.
      This stops the start from ending up after the end. Changing the end on its
      own should still just change the end.

### Community documents for GitHub

- [x] Add a `CODE_OF_CONDUCT.md`, for example based on the Contributor Covenant,
      with a contact for reports (like `SECURITY.md` does for vulnerabilities).
- [x] Add a `CONTRIBUTING.md`: how to set up the devcontainer, run the tests
      (`task test`), install the hooks (`task hooks:install`), the commit and PR
      conventions (see `.github/pull_request_template.md`), and that every UI string
      needs both English and Danish.

### Menu and settings cleanup

- [x] Clean up the menu. Every guardian page is listed once in
      `features/guardian/shell/nav-items.ts`, grouped as Every day, Health &
      well-being, Routines and Family & helpers, and shown by the new **Menu** button
      in the header (`shell/nav-menu/`) and, for the pages with a `tab`, the phone tab
      bar. The profile menu keeps only account items: theme, Settings, Help, Sign out
      and the version.

### Remove the current user's events view

- [x] Remove the events list shown under settings. It was only added early on to
      see what happened. Removed `EventsList` (`features/guardian/events-list/`),
      `UserEventsService` (`core/user-events.service.ts`) and the `events.*`
      translation keys, and the backend endpoint `GET /users/me/events`
      (`Features/Users/ListEvents/`) with its tests, `.http` entries, pagination
      `Cursor` and the `IUserEventStore` read-forward/backward methods.

### Babysitter colors in print templates

- [x] Let print templates set a color for each babysitter, like they already do
      for guardians (`guardianColors`). Today a babysitter pickup always prints
      without a color (`assemble-week-plan.ts`). Needs a picker in the print template
      editor and a backend change to store the colors on the template.

### Sort tasks by time, then name

- [x] Sort the tasks in the print view and the calendar by start time, and when
      two or more start at the same time, by name, so the order is always the same.
      Reuse the locale-aware name sorting in `core/array-utils.ts`.

### GitHub link in the menu

- [x] Add a link to the GitHub repository in the profile menu, next to where the
      version is shown (`features/guardian/shell/profile-menu/`). Consider making the
      URL part of the runtime config, so a family running its own fork can point it
      elsewhere.

### Error status codes in the OpenAPI documents

- [x] Check that every error status code an endpoint can return is documented in
      the OpenAPI documents (compare with `docs/backend/http-status-codes.md`). Checked
      2026-10-09 against the 14 generated documents (170 operations): only `400`, `404`
      and `409` from the endpoints' `Results<...>` show up.
- [x] Fix the gaps the check found. Done 2026-10-09:
      [Client-ready OpenAPI documents](docs/backend/analysis/openapi-client-contract.md). Enums are
      strings on the wire now, the contract is committed as `docs/backend/openapi/buddy.json`, and the
      frontend's API types are generated from it.
  - `403` is missing everywhere: `ForbidHttpResult` and `JsonHttpResult<ErrorEnvelope>`
    (`email_not_verified`) add no response metadata (111 endpoints).
  - Middleware responses aren't documented: `401` and `403 user_not_provisioned`
    (authorized routes), `409 concurrency_conflict` (commands), `400` body-binding /
    `invalid_idempotency_key` and `409 idempotency_key_*` (POSTs), `304` (GETs with
    ETag), `429`, `500`, `503`. No security scheme is declared either.
  - The iCal feeds (`GET /calendars/{id}/ical/{token}`, `GET /mealplans/{id}/ical/{token}`)
    list no `200`: `ContentHttpResult` adds no metadata.
  - The status-code doc is missing 19 routes (AI assistant, `/users/me/export`,
    language/timezone, `PATCH /calendars/{id}/icon`, `PUT /calendars/{id}/group/{groupId}`,
    `GET /calendars/{id}/assignable-members`, `GET /mealplans/groups/{id}/status`,
    `PUT /progress/children/{id}/goals`) and lists `PATCH /calendars/{id}/members/{memberId}`,
    which doesn't exist. Small mismatches: `404` on `POST /babysitters/me`,
    `GET /print-templates` and the work-location writes isn't in the doc.

### Event-stream snapshots rollout

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

### Follow-ups worth doing (2026-10-08)

- [x] Unused `FindSnapshotAsync`: `CalendarItem` and `Meal` now have production
      readers (erasure, export, the AI meal filter). `GuardianLink` and
      `MedicineSchedule` still have none, but kept: every snapshot test reads
      through it, and it's the same store contract as the other aggregates.
- [x] Backfill/rebuild tooling: the API runs the JasperFx command line
      (`projections list|rebuild --store <uri>`), `task db:snapshots:rebuild`
      rebuilds every store, `SnapshotRebuildTests` covers it, and
      `deploy/README.md` has the container command.
- [x] `ValueTupleJsonConverterFactory` handles any tuple of 1–7 items with one
      converter (same JSON shape), covered by `ValueTupleJsonConverterFactoryTests`.

### Claude skills — gap review (2026-09-30)

Current project skills (`.claude/skills/`): `claude-backend`,
`backend-aware-review`, `mutation-fix`. The `agents/` packages (codex,
copilot, github-pilot) are not Claude Code skills (no frontmatter, not under
`.claude/skills/`), so Claude never loads them automatically.

#### Problems in the existing skills

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

#### Missing skills, by priority

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

#### Supporting housekeeping

- [x] Add a root `CLAUDE.md` (run `/init`) with the stack, task commands,
      ports and conventions, so skills can link to it instead of repeating it.
- [x] Decide whether `agents/` or `.claude/skills/` is the canonical home.
      Either generate Claude skills from `agents/` or mark `agents/` as
      Codex/Copilot-only in `agents/README.md`.
- [x] Prune stale `.claude/worktrees/agent-*` checkouts (they hold old
      copies of the skills and TODO). `.claude/worktrees/` no longer exists.
- [x] After each new skill, test that it triggers using `skill-creator`'s
      eval and description-tuning workflow.
      (2026-10-08) Now a standing rule in `CLAUDE.md` under "Project skills".

#### Status (2026-10-01)

All of the skills above are written, plus `CLAUDE.md`. `backend-aware-review`
now uses `dotnet-skills` only when the plugin is installed and falls back to
an inline checklist otherwise. Follow-ups:

- [x] Worktree `.claude/worktrees/agent-a1f407bec71b27f87` is merged but has
      an uncommitted one-line `Program.cs` change. Keep or drop it, then
      `git worktree remove` it. (`agent-a73bc152…` is already removed.)
- [x] Test that the new skills trigger, using `skill-creator` evals.

#### Issues the skill agents found in the codebase

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

#### Fix round (2026-10-01)

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

Follow-ups:

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
- [x] **Decided: resolve the key through any linked child.** The family AI credential is attached to
      one child, so unlinking that child silently drops the family's key. When
      families merge, `ResolveFamilyAiCredentialIdAsync` picks a credential out
      of a `HashSet`, so the choice is effectively random. Move the credential
      to the guardian or family, or resolve it through any linked child.
- [x] Backend mutation survivors: `CreateChild.Validator` FamilyName and
      Username rules aren't covered by tests (found by the first real Stryker
      run).

#### Round 3 (2026-10-01)

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

Follow-ups:

- [x] Six `interactive-supports-focus` lint warnings on click-to-dismiss
      backdrops need an accessibility pass.
- [x] `CreateCalendar`'s `Icon.Value` NotEmpty rule is dead code, because
      the endpoint turns a blank icon into null.

#### Suggested order

Fix `backend-aware-review`'s missing dependency first, because it's broken
today. Then rewrite `claude-backend` for Marten/Wolverine and add
`CLAUDE.md`. Then build `buddy-frontend` (with i18n) and `backend-feature`,
since they cover most day-to-day work. After that, add `e2e-test` and
`run-buddy`, then `feature-from-analysis`, using Sleep diary as its first
real test. `mutation-fix-backend`, `sonar-triage` and `deploy` can come
whenever they're needed.

### Week plan printing and work locations (2026-10-02)

Built and staged: guardian work locations, the print templates backend and the print frontend.
The design and every decision are in
[docs/frontend/analysis/week-plan-printing.md](docs/frontend/analysis/week-plan-printing.md),
[docs/backend/analysis/week-plan-print-templates.md](docs/backend/analysis/week-plan-print-templates.md)
and [docs/backend/analysis/work-locations.md](docs/backend/analysis/work-locations.md).

Before calling it done:

- [x] Commit the staged print-templates backend and print frontend.
- [x] Have a Danish speaker review `translations/da/print.ts` and `translations/da/work-locations.ts`.
      Claude wrote both; check in particular "Forælder" as the label for a guardian.
- [x] Print a real week on paper and compare it with the old fridge sheet.

Found along the way:

- [x] Some e2e specs fail only in a parallel full run: calendar, groups, task library and AI
      settings. They pass when run serially. They are unrelated to printing, but a cold dev server
      with several workers seems to push them over the timeout.
      (2026-10-08) Not reproducible: three full parallel runs (8 workers, cold dev servers each
      time) passed 31/31. Reopen with a trace if it comes back under load.
- [x] The `db:marten:*` tasks and `MARTEN_SCHEMAS` live only in the git-ignored `taskfile.yml`,
      not in `taskfile.dist.yml`. `worklocations` and `printtemplates` were added locally, but other
      clones don't get them. The skills and `CLAUDE.md` still point at `taskfile.yml`.
      Either move the tasks into `taskfile.dist.yml` or document the local setup.
      (2026-10-08) Moved to `taskfile.dist.yml` with `MARTEN_SCHEMAS`; skills and docs point there.

### Bugs found while generating the docs screenshots (2026-10-03)

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
- [x] **The meal plan import page shows a Danish placeholder in the English UI.** The empty
      "Meal plan text" field's placeholder reads `Madplan 2025 / U12 / Sø: Lasagne / Ma:
Fiskefrikadeller m. salat`, while every other string on the page is English. It isn't a
      tooltip problem or e2e/demo data: the screenshot leaves the textarea empty, and the demo
      family's earlier import (`screenshots/demo-family.ts`) uses English meals. The English
      dictionary has a copy of the Danish text: `mealplan.import.input.textPlaceholder` in
      `core/i18n/translations/en/mealplan.ts` is the same as in `da/mealplan.ts`. Fix: give the
      English entry an English sample that the weekly-note parser accepts (it accepts
      `Meal plan 2025`, `Week 12`/`W12`, and English day names such as `Sun:`/`Mon:`; see
      `WeeklyNoteImportFormat.cs`), e.g.
      `'Meal plan 2025\nW12\nSun: Lasagne\nMon: Fish cakes with salad\n…'`, then run
      `task docs:screenshots` to refresh `guardian-mealplan-import`.

Found along the way:

- [x] The full frontend unit suite (`npm test -- --watch=false`) times out in a different set of
      specs on each run when the machine is busy (load average 20–30 on 16 cores, with dev servers,
      language servers and a second agent session running). Every spec passes when run in smaller
      groups (`--include 'src/app/core/**/*.spec.ts'` and so on). The specs wait with `settle()`
      loops and real timers. Consider limiting the Vitest worker count, or raising the per-test
      timeout for the unit-test builder.
      (2026-10-08) `vitest-base.config.ts` (angular.json `runnerConfig`): `maxWorkers: 50%`,
      `testTimeout: 15s`.

### Best-practice gap review (2026-10-06)

#### Security and privacy

- [x] **Containers run as root.** Both Dockerfiles now switch to their `aspnet`/`caddy` user.
      Caddy keeps :80 (the base image gives its binary `cap_net_bind_service`) and owns `/data` and
      `/config`; `aspnet` has a home directory for the Data Protection key ring.
- [x] **Data Protection keys aren't persisted.** The key ring now lives in Postgres (the
      `dataprotection` schema, `Common/DataProtection/DataProtectionFeature.cs`), so it survives
      redeploys and is shared by every replica. API keys stored before this change were encrypted with
      a lost key ring and must be entered again once.
- [x] **Security headers.** The app's headers are in `src/frontend/buddy/Caddyfile` (so they also
      apply on Azure); the API sets its own in `Common/Http/SecurityHeadersMiddleware.cs`. Keycloak
      sends its own.
- [x] **Enforce the app's Content-Security-Policy.** Enforced in `src/frontend/buddy/Caddyfile`
      since the house rules work (their markdown is the first user-authored rich text). The pre-boot
      theme script moved to `public/theme-init.js`, and `inlineCritical` is off in `angular.json`, so
      the production build has no inline script or event handler.
- [x] **Security headers for the API on Azure.** Moved from `deploy/Caddyfile` into the API
      (`SecurityHeadersMiddleware`), so both deployments send them.
- [x] **Least-privilege `permissions:`** (`contents: read`) in every workflow.
- [x] **Pin GitHub Actions by commit SHA** instead of tag, with the release as a trailing comment
      (`@<sha> # v7.0.1`); Dependabot's `github-actions` ecosystem bumps both.

#### Operability

- [x] **Observability.** OpenTelemetry traces, metrics and logs (ASP.NET Core, HttpClient, Npgsql,
      Wolverine, runtime), exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; JSON console
      logs with TraceId/RequestId outside Development. See `docs/backend/observability.md`.
- [x] **More logging.** Audit-style logs for account and access changes, plus logs for swallowed
      and infrastructure failures; IDs only (docs/backend/observability.md, `AuditLogTests`).
- [x] **Global exception handler.** `UnhandledExceptionHandler` turns any unhandled exception into a
      500 `internal_error` (or 503 `dependency_unavailable` with `Retry-After` when Postgres, Keycloak or
      SMTP can't be reached) `ErrorEnvelope`, logged with the matching requestId.
- [x] **Readiness health checks.** `/health/ready` checks Postgres (503) and Keycloak (degraded,
      still 200); the prod compose file probes it for `api` and `/` for `frontend`.
- [x] **Health probes on Azure Container Apps.** `deploy/azure/deploy.sh` gives the API startup and
      liveness probes on `/health` and a readiness probe on `/health/ready`; Keycloak is probed on its
      management port (9000) and the frontend on `/`.
- [x] **Pin the base images.** Official `sdk:11.0.100-preview.7` / `aspnet:11.0.0-preview.7`
      (matching `global.json`) instead of floating nightlies; every base and compose image pinned by
      digest, refreshed by Dependabot.

#### Build reproducibility

- [x] **Pin the .NET SDK.** `global.json` at the repo root pins the devcontainer's SDK, and the
      workflows install it via `global-json-file` (CI was on rc.1 while the devcontainer ran preview.7).
- [x] **Pin the Node version.** `.nvmrc` (24) read by CI and matched by the Dockerfile,
      `engines` + `engine-strict`, and Dependabot no longer bumps the Node major.
- [x] **Drop `--legacy-peer-deps`.** It hid no conflicts; `npm ci` resolves cleanly without it.
      Both Docker build contexts now have a `.dockerignore`, so local `node_modules`/`bin`/`obj` can't
      leak into an image.
- [x] **Enable .NET analyzers.** `latest-recommended` plus code style enforced on build, findings
      fixed; deliberate exceptions are in `src/backend/.editorconfig`.
- [x] **`npm audit` vulnerabilities in the frontend dependencies.** Fixed in `86f4c8ef` by lockfile
      bumps, plus an `@parcel/watcher` override in the Keycloak theme build; both lockfiles audit
      clean. The CI gate that stops new ones is the security-scanning item.

#### Process

- [x] **Code coverage in CI** for backend and frontend: job summary, a PR comment per side, and
      the full report as an artifact (backend 89% lines / 70% branches, frontend 97% / 92%).
- [x] **Repo hygiene files.** MIT `LICENSE`, `SECURITY.md` (GitHub private reporting),
      `.github/CODEOWNERS` and a PR template.
- [x] **Enable private vulnerability reporting** on `kimipsen/buddy` (the primary repo);
      `SECURITY.md` points reporters there.
- [x] **Pre-commit lint/format hook** in `.devcontainer/git-hooks` (installed by
      `task hooks:install`): Prettier, ESLint, i18n parity and C# whitespace on staged files.

### Initial bundle over budget (2026-10-09)

- [x] **Bring the initial bundle back under its 500 kB warning budget.** The production build
      warns: `bundle initial exceeded maximum budget. Budget 500.00 kB was not met by 23.05 kB with a
total of 523.05 kB.` (budget in `src/frontend/buddy/angular.json`). Find what pulled code into
      the initial chunk (`ng build --stats-json` plus a bundle analyzer), and move it out with lazy
      routes, `@defer` blocks or lighter imports. Don't just raise the budget.
      Done 2026-10-10: Danish is a lazy dictionary chunk and the email/share-link pages are lazy
      routes; the initial bundle is 439 kB.

### Tools for families with ADHD and autism (2026-10-11)

Ideas for new features beyond what Buddy already has. The four marked **(start here)** shipped on 2026-10-13:
they reuse data the app already keeps. Each has a design doc.

#### In-the-moment help for the child

- [x] **Visual time-left timer (start here).** A shrinking disc or bar on the child's current
      task and subtask, counting down the durations the task library already stores. See
      [Visual time-left timer](docs/frontend/analysis/visual-time-left-timer.md).
- [x] **Transition warnings (start here).** A heads-up before calendar events and pickups ("10
      minutes until we leave for football"), configurable per child. See
      [Transition warnings](docs/backend/analysis/transition-warnings.md).

#### Regulation and emotions

- [x] **Feelings check-in (start here).** The child taps one of four colour zones (each with a
      face and a word) a few times a day; guardians see trends, with a doctor share link like the sleep diary's. See
      [Feelings check-in](docs/backend/analysis/feelings-check-in.md).

#### Insight for guardians and clinicians

- [x] **Medicine effect and side-effect notes.** Ratings ("focus good / appetite poor") linked to
      doses, for dose-adjustment conversations. Shipped 2026-10-14 as `/guardian/medicine/effects`.
      See [Medicine effect and side-effect notes](docs/backend/analysis/medicine-effect-notes.md).

#### School and other caregivers

- [x] **Packing checklists (start here).** Weekday-bound checklists ("Tuesday: gym clothes") on the
      child's day view. See [Packing checklists](docs/backend/analysis/packing-checklists.md).

#### Smaller ideas

- [x] **Homework splitter.** Break an assignment into timed focus chunks with breaks. See
      [Homework splitter](docs/frontend/analysis/homework-splitter.md).

### Toggle the meal plan in calendar views (2026-10-14)

- [x] Add a toggle to show or hide the meal plan in the calendar views (default: on). It's a
      "Meal plan" switch in the child calendar's filter bar, remembered per device. Only the
      child calendar shows meals (2026-10-14).

### Calling names for guardians (2026-10-14)

- [x] Let guardians/parents be identified by a calling name instead of only their first name,
      for example "Dad" / "Far" or "Mom" / "Mor". See
      [guardian-calling-names.md](docs/backend/analysis/guardian-calling-names.md).

### Tools for families with ADHD and autism (2026-10-11)

#### In-the-moment help for the child

- [x] **First–Then board.** Pair a non-preferred task with a preferred activity ("first homework,
      then iPad"). See [First–Then board](docs/backend/analysis/first-then-board.md) (2026-10-14).

### Skip single occurrences of repeating appointments and tasks (2026-10-14)

- [x] Add exclude/skip events for both appointments and tasks, so a guardian can skip one
      occurrence of a repeating appointment or task on an individual day without changing or
      ending the series. See
      [skip-occurrences.md](docs/backend/analysis/skip-occurrences.md) (2026-10-15).

### Security and privacy (best-practice gap review, 2026-10-06)

- [x] **Security scanning in CI.** CodeQL (`codeql.yml`) plus `security-scan.yml`: NuGet and npm
      vulnerability gates and a Trivy scan of the API, web and Keycloak images, on changes and
      daily. Getting it green pinned `System.Security.Cryptography.Pkcs` 10.0.13 (via MailKit), moved
      .NET 11 from preview.7 to rc.2 (critical runtime CVEs), kept the theme tooling out of the
      Keycloak image and baselined three upstream Go CVEs in `.trivyignore` until 2026-11-15
      (2026-10-15).

### Tools for families with ADHD and autism (2026-10-11)

#### In-the-moment help for the child

- [x] **Pictogram support.** Bundled pictograms as the icon of tasks, routine steps and First-Then
      cards, for younger or non-reading children. See
      [Pictogram support](docs/frontend/analysis/pictograms.md) (2026-10-16).

### Tools for families with ADHD and autism (2026-10-11)

#### Insight for guardians and clinicians

- [x] **Doctor visit summary.** One printable, shareable report of sleep, doses, effect notes and
      feelings for a chosen period of at most 92 days, with a revocable, expiring doctor link. See
      [Doctor visit summary](docs/backend/analysis/doctor-visit-summary.md) (2026-10-16).

### Backend unit tests as the primary test source (2026-10-16)

- [x] **Phase 0: stop the out-of-memory errors.** Wolverine handler code is pre-generated and
      committed (`task codegen:backend`, checked in CI), extra test hosts are shared per class
      (`BuddyApiFixture.SharedHostAsync`) and Keycloak is capped. A full run peaks at about 0.9 GB
      in the test process instead of 7.7 GB+ (2026-10-16).
- [x] **Inject the clock.** All production code takes an injected `TimeProvider` (handlers,
      services, static helpers); `DateTimeOffset.UtcNow` and friends are banned in the API project
      (`buddy/BannedSymbols.txt`, build error RS0030) (2026-10-16).

### Document all validations (2026-10-16)

- [x] Make the documentation list every validation rule: each of the 93 FluentValidation
      validators in `src/backend/buddy/Features`, plus the domain checks in the handlers, belongs
      in its domain's `docs/backend/<domain>/flow.md` (field, rule, error code, HTTP status) and
      matches `docs/backend/openapi/buddy.json`. `task docs:validation` (and backend CI) checks the
      tables against the contract (2026-10-19).

### Validation gaps found while documenting (2026-10-19)

- [x] **Error keys that don't match the request.** First-Then cards report `First.first.title` /
      `Then.then.icon` (the card validator's override plus the parent prefix); `LogSleepEntry`,
      `UpdateCurrentEmail` (`Value`), `UpdateOnboardingProgress` (`ExpectedVersion`),
      `StartAiSession` (`RequestedSlots`) and `PreviewMealPlanImport` (`Options.*`) use command
      property names; template icons use both `Icon` and `Icon.Value`. Keys mix PascalCase and
      camelCase across domains. Every key is now the camelCase
      JSON path of the request field (`ValidationProblem` normalizes them).
- [x] **Unvalidated fields.** Icons follow the shared icon rule and colors are capped at 32
      characters on meals, medicines, calendars, calendar items and task templates; edited item
      titles, group names, the guardian's own name and invite emails are validated; undefined enum
      values (dose status, recurrence frequency, roles, child kind, permission policies) are
      rejected; inviting as group owner is a 400 under `role` (2026-10-19).
- [x] **Probable 500s and leaks.** A `null` goal post is a 400, and `StartAiSession` only checks
      must-include meals of the family (2026-10-19).
- [x] **Untrimmed names.** Meal, medicine, calendar, task-template and subtask names, the
      provider API key (now at least 8 characters) and email changes are trimmed before
      validation and storage (2026-10-19).

### Publishing Docker images

- [x] **Phase 1: publish.** `publish-images.yml` builds the api, frontend and keycloak images for
      amd64 and arm64 on native runners, scans each with Trivy, and publishes them to GHCR with
      release, `edge` and `sha-` tags, build provenance and an SBOM. The frontend image reads
      `API_BASE_URL`/`KEYCLOAK_AUTHORITY` at container start instead of at build time
      (2026-10-19).
- [x] **First run.** `publish-images.yml` published `v1.5.3`; the three `buddy-*` packages are
      public (an anonymous pull of `ghcr.io/kimipsen/buddy-frontend:1.5.3` works) and carry
      `linux/amd64` and `linux/arm64` (2026-10-20).
- [x] **Phase 2: deploy from the published images.** `task deploy:pull` (`deploy/deploy-pull.sh`:
      pulls the release images, `up -d --no-build --wait`, rollback by checking out the earlier
      tag) and `BUDDY_IMAGE_VERSION` in `deploy/azure/.env` (`az acr import` instead of
      `az acr build`) (2026-10-20).

### Leave medicines out of the effect summary (2026-10-19)

Some medicines (vitamin D, melatonin) aren't relevant to an ADHD doctor visit, but they split dose
periods and clutter the summary. Design in
[Leaving medicines out of the effect summary](docs/backend/analysis/medicine-effect-inclusion.md).

- [x] Settle the open questions: the shared summary says how many medicines were left out, without
      names; the switch lives only on the effects page (2026-10-21).
- [x] Backend: `MedicineEffectInclusionChanged`, `SetMedicineEffectInclusion`, the flag on the
      schedule and regimen responses, and filtering in `MedicineVisitSummarySource`.
- [x] Frontend: switches in the "By dose period" card, `groupByDosePeriod` skipping excluded
      medicines, the "Not included" line, en+da strings, specs, e2e and screenshots (2026-10-21).

### iCal feed of my pickups and drop-offs (2026-10-22)

- [x] **Analysis.** Write `docs/backend/analysis/pickup-ical-feed.md`: a per-guardian token (not
      per child), which assignments are included (only the ones to the current guardian), event
      title/location, recurrence vs. expanded occurrences, VALARM, and revocation (2026-10-23).

### SBOM generation (2026-10-20)

Plan in [SBOM generation for builds](docs/backend/analysis/sbom-generation.md).

Generate a software bill of materials for every build and upload it to GitHub next to the build
output.

- [x] **Docker images.** Generate an SBOM for each image in `publish-images.yml` (e.g. BuildKit's
      `sbom: true` attestation or Syft) and attach it to the pushed image and the release.
- [x] **Backend.** Generate a CycloneDX SBOM of the .NET NuGet dependencies in the backend build
      and upload it as a workflow artifact.
- [x] **Frontend.** Generate a CycloneDX SBOM of the npm dependencies in the frontend build and
      upload it as a workflow artifact.
- [x] **Releases.** Attach the backend, frontend and image SBOMs to the GitHub release as assets.
- [x] **First release run.** Push the next tag (a pre-release such as `v1.6.2-rc.1` is enough) and
      check the run's `sbom` artifact, the eight assets on the release (and that the release is a
      pre-release), and
      `gh attestation verify oci://ghcr.io/kimipsen/buddy-frontend:<version> --repo kimipsen/buddy --predicate-type https://cyclonedx.org/bom`. Done on `v1.6.2-rc.1` (2026-10-23).

### Installation instructions in the README (2026-10-22)

The root README only explains how to run Buddy in the development container. Add instructions on
how to install Buddy.

- [x] **Install section.** Add an installation section to `README.md` covering the prerequisites,
      the published Docker images, the `.env` settings and the first Keycloak setup, and link the
      [VM deploy guide](deploy/README.md) and the [Azure deploy guide](deploy/README-azure.md). Done, with the full
      [installation guide](docs/installation.md) (2026-10-23).

### iCal feed of my pickups and drop-offs (2026-10-22)

Give each guardian a private iCal subscription link with the pickup and drop-off slots they are
assigned to, across all their children, so the slots show up in their own calendar app. Design in
[Pickup iCal feed](docs/backend/analysis/pickup-ical-feed.md).

- [x] **Backend.** Token create/list/revoke slices and the feed endpoint in `Features/Pickups`,
      built on `PickupScheduleExpansion`, with integration tests (2026-10-23).
- [x] **Frontend.** Subscription link (copy, revoke) for the guardian, with en + da strings, and
      updated screenshots (2026-10-23).
