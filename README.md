# Buddy

Buddy is a family coordination tool for parents and guardians of children with
ADHD. It provides shared calendars, medication tracking with dose status, and
family meal and pickup planning, all designed around the core insight that kids
with ADHD often struggle less with knowing *what* to do than with keeping track
of *when* and *what's next*. Guardians can plan structure and daily routines,
while children see a personalized day view that helps them stay on track.

## Why Buddy

The challenge isn't usually knowing "I need to take this medicine" or "we're
eating dinner"—it's remembering when, and then actually doing it. Buddy is
built around that reality: shared calendars for events and tasks, structured
medication schedules with daily dose tracking, and a family meal library so
guardians can plan meals once and have them show up for every child. Pickup and
drop-off schedules make responsibility explicit, including guardian, sibling,
self-escort, and playdate arrangements. The child gets a clear, interactive
view of their day; the guardian gets visibility and the ability to adjust plans
in real time.

## Features

### Guardian

- [ ] Guided setup for guardians with no groups or children (proposed, not yet
  implemented -- see [Guardian onboarding](docs/frontend/analysis/guardian-onboarding.md))
- [x] Create child accounts and manage guardian/child relationships
- [x] Shared calendars: day, work-week, rolling-week, and month views, with
      event/task creation across personal and group-owned calendars
- [x] Medicine schedules with daily dose times and dose-status tracking
- [x] Family meal library and meal-plan assignment, with meal ratings and
      group-shared meal plans
- [x] Private iCal subscription links for the family's meal plan
- [x] Chat-based AI assistant that drafts meal-plan assignments, with
      BYOK AI provider settings management
- [ ] Limit the meals sent to the AI assistant to rated meals and/or meals served
      in the last 30/60/90 days (proposed, not yet implemented — see
      [AI assistant meal filter](docs/backend/analysis/ai-assistant-meal-filter.md))
- [x] Import historical meal plans from notes and CSV files, with review and undo
- [x] Pickup and drop-off scheduling (guardian, sibling, self-escort,
      playdate, and babysitter assignments)
- [x] Reusable task library with ordered, timed subtasks, schedulable onto a
      calendar
- [x] Group and calendar sharing/permissions management
- [x] Configurable goal posts and gamified progress tracking for children
- [x] Profile, calendar, group, child, and account administration
- [x] Sleep diary logging for children, with a revocable, expiring share link
      that lets a doctor read a printable 14-day view without an account
- [x] Printable A3/A4 landscape week plans from saved, shareable templates
- [x] Guardian work locations with alternating weekly patterns and per-day
      exceptions
- [x] Saved babysitters/nannies for pickup and drop-off

### Child

- [x] Personalized day view: meals and ratings, medicine doses,
      pickup/drop-off assignments, and tasks
- [x] Read-only seven-day calendar agenda
- [x] Browse current and previous meal-plan weeks and rate meals
- [x] View linked guardians
- [x] Complete tasks from the day view
- [x] View progress and unlocked milestones

### Platform

- [x] Keycloak authentication, token lifecycle, and role-based routing
- [x] Group invitations and email-verification flows
- [x] English and Danish localization
- [x] Light, dark, and system theme selection
- [x] Per-user and per-feed rate limiting on every API endpoint
- [x] GDPR: account and child erasure, data export, AI data minimization and retention, and
      health-data read audit logs. What you still have to do when you run Buddy is in
      [PRIVACY.md](PRIVACY.md)

## Repository structure

```
src/
  backend/   .NET API with event-sourced aggregates, Marten/PostgreSQL storage,
             and Keycloak-backed authentication
  frontend/  Angular application for guardians and children
docs/
  backend/   Backend glossary, flow docs, HTTP semantics, and design analyses
  frontend/  Frontend architecture, feature status, and design analyses
agents/      Reusable agent skills, examples, templates, and samples
deploy/      Production Docker Compose and Caddy deployment
```

- [Architecture](docs/architecture.md) — C4 context and container diagrams.
- [Backend documentation](docs/backend/README.md) — domain glossary, user
  flows, HTTP semantics, and design analysis documents.
- [Frontend documentation](docs/frontend/README.md) — Angular app shell,
  auth flow, feature layout, and current product status.
- [Development container](.devcontainer/README.md) — local services,
  environment setup, ports, and first-run Keycloak configuration.
- [Git hooks](.devcontainer/git-hooks/README.md) — opt-in `pre-commit`
  format/lint checks and a `post-commit` hook that uses Claude, Codex, or
  GitHub Copilot to keep `docs/` and this README in sync with commits.
- [Screenshots](docs/screenshots/README.md) — every page of the app with demo
  data; regenerate with `task docs:screenshots`.
- [Testing](docs/testing.md) — commands for frontend, backend, and mutation
  test suites.
- [Deployment](deploy/README.md) — production Docker Compose and Caddy setup.
- [Privacy and GDPR](PRIVACY.md) — what Buddy does with children's data, and the checklist for
  anyone who runs it.
- [Versioning](docs/versioning.md) — release tags (`v1.2.0`) and how the version
  reaches the API (`GET /version`) and the frontend.
- [Agent packages](agents/README.md) — reusable coding and documentation skills.
- [Backend app guide](src/backend/buddy/README.md) — running, configuring, and
  laying out the API implementation.
- [Frontend app guide](src/frontend/buddy/README.md) — running, configuring,
  and laying out the Angular frontend.
- [docs/README.md](docs/README.md) — the documentation landing page.

## Core concepts

- **User** — an authenticated person (guardian or child), backed by
  Keycloak for authentication and modeled locally as an event-sourced
  aggregate with profile and email state. See the
  [glossary](docs/backend/glossary.md).
- **Calendar** — a scheduling container of events and tasks, owned by a
  user or a group, with per-member roles such as `Owner`, `Contributor`, and
  `Viewer`.
- **Group** — a collection of users, such as a family, with roles such as
  `Owner`, `Admin`, and `Member`, allowing shared ownership and permission
  mapping across calendars. See
  [Group-owned calendars and permissions](docs/backend/analysis/group-owned-calendars-and-permissions.md).
- **Guardian/child relationships** — how a guardian is linked to a child's
  account and what that grants them. See
  [Child accounts and guardian/parent roles](docs/backend/analysis/child-accounts-and-guardian-roles.md).
- **Medicine schedule** — a child-facing medication routine with
  daily dose times, dose tracking, and per-dose `Taken` / `Skipped` states.
  See [Medicine schedules](docs/backend/analysis/medicine-schedules.md).
- **Meal plan** — a family meal library and daily slot assignments for
  breakfast, lunch, dinner, and snacks, with meal rating and archival
  functionality.
- **Pickup schedule** — a per-child weekly plan for pickup and drop-off slots,
  with explicit guardian, sibling, self-escort, playdate, and babysitter
  assignments. See [Pickup and drop-off schedules](docs/backend/analysis/pickup-schedules.md).
- **Babysitter list** — each guardian's saved babysitters and nannies, which any
  of a child's guardians can pick for a pickup slot. See
  [Babysitters](docs/backend/analysis/babysitters.md).
- **Task library** — a child-specific collection of reusable task templates
  with ordered, timed subtasks that a guardian can schedule onto a calendar.
  See [Task Library flow](docs/backend/task-library/flow.md).

## Documentation map

### Backend docs

- [Users flow](docs/backend/users/flow.md)
- [Calendars flow](docs/backend/calendars/flow.md)
- [Groups flow](docs/backend/groups/flow.md)
- [Guardians flow](docs/backend/guardians/flow.md)
- [Medicines flow](docs/backend/medicines/flow.md)
- [Mealplans flow](docs/backend/mealplans/flow.md)
- [Pickups flow](docs/backend/pickups/flow.md)
- [Babysitters flow](docs/backend/babysitters/flow.md)
- [Task Library flow](docs/backend/task-library/flow.md)
- [Progress flow](docs/backend/progress/flow.md)
- [Glossary](docs/backend/glossary.md)
- [HTTP status code semantics](docs/backend/http-status-codes.md)
- [Health checks and observability](docs/backend/observability.md)

### Design analyses

- [GDPR: erasure, export and data minimization](docs/backend/analysis/gdpr-data-protection.md)
- [Group-owned calendars and permissions](docs/backend/analysis/group-owned-calendars-and-permissions.md)
- [Aggregate roots and their relationships](docs/backend/analysis/aggregate-roots.md)
- [All-day calendar items](docs/backend/analysis/calendar-all-day-items.md)
- [Integration testing strategy](docs/backend/analysis/integration-testing-strategy.md)
- [Mutation testing strategy](docs/backend/analysis/mutation-testing-strategy.md)
- [Validation rules](docs/backend/analysis/validation-rules.md)
- [Child accounts and guardian/parent roles](docs/backend/analysis/child-accounts-and-guardian-roles.md)
- [Guardian-managed child language](docs/backend/analysis/child-language-settings.md)
- [Guardian-managed child time zone](docs/backend/analysis/child-timezone-settings.md)
- [Medicine schedules](docs/backend/analysis/medicine-schedules.md)
- [Meal plans](docs/backend/analysis/mealplans.md)
- [Group-shared meal plans](docs/backend/analysis/group-owned-mealplans.md)
- [Meal plan iCal feed](docs/backend/analysis/mealplan-ical-feed.md)
- [Importing historical meal plans](docs/backend/analysis/mealplan-import.md)
- [AI assistant meal filter](docs/backend/analysis/ai-assistant-meal-filter.md)
- [Pickup and drop-off schedules](docs/backend/analysis/pickup-schedules.md)
- [Babysitters](docs/backend/analysis/babysitters.md)
- [Gamified progress](docs/backend/analysis/gamified-progress.md)

## Getting started

The supported local environment is the VS Code development container. It
provides .NET, Node, npm, Angular CLI, Docker, PostgreSQL tooling, and the
service network expected by the checked-in development configuration.

1. Create the local environment file before opening the container:

  ```bash
  cp .devcontainer/.env.example .devcontainer/.env
  ```

2. Open the repository in VS Code and run **Dev Containers: Reopen in
  Container**. On a first run, complete the PostgreSQL and Keycloak setup in
  the [development container guide](.devcontainer/README.md); the current
  Compose file does not create the `keycloak` database or import the `buddy`
  realm automatically.

3. Start the backend:

```bash
cd src/backend/buddy
dotnet run
```

  See the [backend app guide](src/backend/buddy/README.md) for configuration
  details and project layout.

4. In another terminal, install dependencies and start the frontend:

```bash
cd src/frontend/buddy
npm install
npm start
```

The frontend is available at `http://localhost:4300`. See the
[frontend app guide](src/frontend/buddy/README.md) for build and runtime
configuration details, and the [testing guide](docs/testing.md) for test
commands.

## License and security

Buddy is released under the [MIT License](LICENSE). To report a security
problem, follow [SECURITY.md](SECURITY.md); please don't open a public issue.
