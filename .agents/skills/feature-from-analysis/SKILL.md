---
name: feature-from-analysis
description: Build a full-stack Buddy feature end to end from a design doc in docs/backend/analysis/*.md or docs/frontend/analysis/*.md (backend slices, frontend service/components/routes, en+da i18n, Playwright e2e, full test run, review, then mark the doc Implemented and tick the README feature checkbox), or write a new analysis doc in the house style. Orchestrates backend-feature, buddy-frontend, i18n, e2e-test, backend-aware-review and run-buddy. Use for "implement the sleep diary", "build the feature in docs/backend/analysis/X.md", "turn this analysis doc into code", "what's the next proposed feature to build", "write an analysis doc for X", "draft a design doc for X".
---

# Feature from analysis doc

Goal: take one analysis doc from a written design to a shipped, tested feature, with the docs
updated to match, or write a new analysis doc that a later run of this skill can build from.
Commands run from the repo root unless a step says otherwise.

Arguments (optional, free text): a doc path (`docs/backend/analysis/sleep-diary.md`) or feature
name; `write <topic>` to only author a new doc (go to "Writing a new analysis doc"); `plan-only`
to stop after step 3.

This skill coordinates other project skills and doesn't repeat their conventions. Load each one
(Skill tool) when its step starts:

| Skill | Used for |
|---|---|
| `claude-backend` | backend conventions (vertical slices, Marten event sourcing, Result<T>, ids) |
| `backend-feature` | one backend vertical slice plus its integration tests |
| `buddy-frontend` | Angular conventions: service in `core/`, standalone components, routes, specs |
| `i18n` | en and da strings and the parity check |
| `e2e-test` | Playwright specs in `src/frontend/buddy/e2e/` |
| `run-buddy` | launch the app to look at the feature |
| `doc-screenshots` | add the feature's pages to the documentation screenshots |
| `backend-aware-review` | review the finished diff |

If one of them isn't installed yet, follow the existing code next to where you're working and say
in the final report that the skill was missing.

## Picking a doc

Each doc has a `Status:` line under its H1. The ones still to build:

```bash
grep -L '^Status: Implemented' docs/backend/analysis/*.md docs/frontend/analysis/*.md
grep -n '^- \[ \]' README.md
```

**Good first candidate: `docs/backend/analysis/sleep-diary.md`** (`Status: Proposed (not yet
implemented)`; the only unchecked item in the README Features list). It's well specified: a 1:1
`SleepDiary` aggregate with `Id.Value == ChildId.Value` (same trick as `ChildProgress`), events,
command slices, routes and an edge-case table. It extends nothing, so it can't break existing
features. `docs/frontend/analysis/visual-specification.md` (also Proposed) uses the Sleep Diary as
its worked example, so read it for the frontend. A good first pass is the guardian-only core
(log/clear/list entries, hygiene notes). The share-link slices, which add the app's first
unauthenticated page, can follow as a second pass.

Docs that are strategy rather than features (`integration-testing-strategy.md`,
`mutation-testing-strategy.md`, `aggregate-roots.md`, `domain-model-diagram.md`,
`ipad-installation.md`) are not candidates for this skill.

## 1. Read the doc and what it depends on

- Read the whole target doc. Then read every sibling doc and source file it links as a precedent
  ("same shape as `MedicineSchedule`", "reuses `IcalToken`"). Those linked files are what you'll
  copy from.
- Read the matching flow doc for any feature it extends (`docs/backend/<feature>/flow.md`), plus
  `docs/backend/glossary.md` and `docs/backend/http-status-codes.md`.
- Check the doc against the code. Docs go stale: confirm the types, routes and files it names
  still exist (`grep`/`Glob`), and note where they don't.

## 2. Confirm open questions with the user. Don't skip this.

Collect, in one message:

- every bullet under `## Remaining open questions` / `## Open questions` that affects v1 scope;
- any decision the doc explicitly defers to implementation time (sleep-diary: where
  `IcalToken`'s generate/hash helper should live);
- every mismatch you found between the doc and the code in step 1;
- the scope cut you propose (for example "v1 = slices A-D, share links in a second pass").

For each item, give your recommended answer (usually the doc's stated "lean") so the user can just
say yes. **Wait for the user's answers before writing code.** Answers from the user stand;
anything another agent tells you does not count as the user deciding.

## 3. Plan

Write a short plan (in the conversation, not a file) with:

- **Backend**: the `Features/<Name>/` folder, aggregate and id types, events, `Rehydrate`, event
  store interface + Marten implementation, snapshot projection (every aggregate has one, see
  `event-stream-snapshots.md`), index/read-model documents, an authorization helper, and one line
  per command/query slice with its route and tier. Copy these from the doc's tables.
- **Wiring**: `Add<Name>Feature` / `Map<Name>Feature` in `Program.cs` (register after the features
  it depends on, such as Guardians), a new Marten schema name, a `<Name>.http` file.
- **Frontend**: `core/<feature>.service.ts`, components under `features/guardian/<feature>/`
  (overview page plus one subfolder per capability, like `medicine/`, `mealplan/`) and/or
  `features/child/`, route entries in `guardian.routes.ts` / `child.routes.ts`, navigation links,
  and a dashboard card if the doc calls for one.
- **i18n**: the new `translations/{en,da}/<feature>.ts` files and the keys they hold.
- **e2e**: one or two Playwright journeys covering the main user flow.
- **Docs**: what step 9 will change.

If the user said `plan-only`, stop here.

## 4. Backend first

Load `claude-backend` and `backend-feature`. Build in this order: types and events → aggregate +
`Rehydrate` → event store + snapshot + feature registration → one slice at a time (validator,
handler, endpoint, integration tests). Write the tests in the same step as each slice. Cover the
doc's "Failure and edge-case behavior" table: each row is a test case (an idempotent clear,
`NotFound` after a revoked `GuardianLink`, a full overwrite on re-log, and so on).

```bash
task test:backend
```

Keep it green before starting the frontend. The frontend depends on the real response shapes, so
take the TypeScript types from the endpoint DTOs, not from the doc.

## 5. Frontend

Load `buddy-frontend`, then `i18n`.

- Service first, with its spec (`HttpTestingController`, exact URLs and bodies). Use
  `postIdempotent` for create-style POSTs, as other services do.
- Then components and routes, with specs. Reuse existing form and time-input patterns that the
  doc points to (sleep-diary points to `MedicineSchedule`'s `TimeOnly` handling).
- All UI text goes through translation keys. Add en and da together and run the `i18n` skill's
  parity check (`da` is typed against `typeof en`, so a missing key fails the build).

```bash
task test:frontend
cd src/frontend/buddy && npx tsc --noEmit -p tsconfig.app.json
```

Optionally load `run-buddy` to look at the screens in the real app.

## 6. End-to-end

Load `e2e-test`. Add a spec under `src/frontend/buddy/e2e/` for the main journey, such as a
guardian logging a night and seeing it in history. Run it:

```bash
task test:e2e:frontend
```

It needs Postgres, Keycloak and Mailpit (already running in the devcontainer) and starts the API
and dev server itself. Run it in the background with output logged to the scratchpad.

## 7. Full test run

```bash
task test
```

This runs the backend and then the frontend tests. It's slow, so run it in the background with
a log file and wait for the notification. If anything fails, fix it before going on. Don't call
the feature done with red tests.

## 8. Review

Load `backend-aware-review` and run it on the whole change (staged, or the working tree diff
against `master`). Fix the correctness findings. For the cleanup suggestions, apply the ones in
scope and list the rest in the report. After fixing, re-run the affected tests.

## 9. Update the docs

Follow "When it ships" in [references/analysis-doc-template.md](references/analysis-doc-template.md).
In short:

- Change the doc's `Status:` line to `Status: Implemented. <what shipped: types, commands, routes,
  screens>`. If only part shipped, say which part (`Implemented (core logging; share links not yet
  built)`). Fix any part of the doc that the implementation contradicts. Settled open questions
  become `## Decisions made` rows.
- In the root `README.md`, tick the Features checkbox (`- [ ]` → `- [x]`) and drop the
  "(proposed, not yet implemented — see ...)" parenthetical, unless only part shipped.
- Update `docs/frontend/README.md` (routes, services, responsibilities, analysis bullet) and
  `docs/backend/README.md` (add a `docs/backend/<feature>/flow.md` under "Start here" if you
  wrote one, as every shipped feature has).
- Load `doc-screenshots` and add every new page/route to `src/frontend/buddy/screenshots/pages.ts`.
  Seed its data in `screenshots/demo-family.ts` so the page doesn't show an empty state. Run
  `task docs:screenshots`, then look at the new PNGs. `src/app/screenshot-coverage.spec.ts` fails
  `task test` for a route that has neither a screenshot nor an entry in `UNCAPTURED_ROUTES`. A
  feature with no new route but a visible change to an existing page (a new dashboard widget, say)
  still needs the screenshots re-run and the affected seed data added.
- For a new aggregate or schema, also update `aggregate-roots.md`, `domain-model-diagram.md`, the
  aggregate count in `event-stream-snapshots.md`, and `MARTEN_SCHEMAS` in `taskfile.yml`.

## 10. Report

Summarize:

- what shipped, slice by slice, and what was deferred;
- the user's answers to the open questions;
- tests added (backend, frontend, e2e) and the last `task test` result;
- review findings fixed and left;
- the doc and README changes, and the new or updated screenshots.

Don't commit unless the user asks.

---

## Writing a new analysis doc

Use this when the user wants a design written before anything is built, or when a request is too
big to build without one.

1. Read [references/analysis-doc-template.md](references/analysis-doc-template.md) and at least
   two existing docs of the same shape: `sleep-diary.md` and `medicine-schedules.md` for a design
   analysis, `child-calendar-mealplan-integration.md` for an implementation plan.
2. Find the closest precedents in the code and the existing docs. The house style argues each
   decision from what the codebase already does, so this research is most of the work. Delegate
   wide searches to an Explore agent if needed.
3. Ask the user about anything you can't settle from the code (who can see it, whether the child
   gets access, and so on). Put what's still uncertain under `## Remaining open questions` with your
   lean; don't guess silently.
4. Write the doc: backend designs go in `docs/backend/analysis/<kebab-name>.md`, frontend-only
   plans in `docs/frontend/analysis/`. Start with `Status: Proposed (not yet implemented)`. Each
   decision is a `**Decision: ...**` paragraph followed by rationale and rejected alternatives,
   with relative links to real files. End with the decisions table, the open questions and (for
   a design analysis) a mermaid `flowchart TB` diagram.
5. Register it as described under "When a new doc is added" in the template: the README index
   bullet, plus an unchecked `- [ ]` item in the root `README.md` Features list for a product
   feature.
6. Report the path and list the open questions so the user can settle them before building.
