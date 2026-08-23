# Buddy

Buddy is a family coordination tool for parents and guardians of children with
ADHD. It provides shared calendars, medication tracking with dose status, and
family meal planning—all designed around the core insight that kids with ADHD
often struggle less with knowing *what* to do than with keeping track of
*when* and *what's next*. Guardians can plan structure and daily routines
(schedules, medications, meals), while children see a personalized day view
that helps them stay on track.

## Why Buddy

The challenge isn't usually knowing "I need to take this medicine" or "we're
eating dinner"—it's remembering when, and then actually doing it. Buddy is
built around that reality: shared calendars for events and tasks, structured
medication schedules with daily dose tracking, and a family meal library so
guardians can plan meals once and have them show up for every child. The child
gets a clear, interactive view of their day; the guardian gets visibility and
the ability to adjust plans in real time.

## Repository structure

```
src/
  backend/   .NET API with event-sourced aggregates, Marten/PostgreSQL storage,
             and Keycloak-backed authentication
  frontend/  Angular application for guardians and children
docs/
  backend/   Backend glossary, flow docs, HTTP semantics, and design analyses
```

- [Backend documentation](docs/backend/README.md) — domain glossary, user
  flows, HTTP semantics, and design analysis documents.
- [Frontend documentation](docs/frontend/README.md) — Angular app shell,
  auth flow, feature layout, and current product status.
- [src/backend/buddy](src/backend/buddy) — the API implementation.
- [src/frontend/buddy](src/frontend/buddy) — the Angular frontend.
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
- **Medicine schedule** — a proposed child-facing medication routine with
  daily dose times, dose tracking, and per-dose `Taken` / `Skipped` states.
  See [Medicine schedules](docs/backend/analysis/medicine-schedules.md).
- **Meal plan** — a family meal library and daily slot assignments for
  breakfast, lunch, dinner, and snacks, with meal rating and archival
  functionality.

## Documentation map

### Backend docs

- [Users flow](docs/backend/users/flow.md)
- [Calendars flow](docs/backend/calendars/flow.md)
- [Groups flow](docs/backend/groups/flow.md)
- [Guardians flow](docs/backend/guardians/flow.md)
- [Medicines flow](docs/backend/medicines/flow.md)
- [Mealplans flow](docs/backend/mealplans/flow.md)
- [Glossary](docs/backend/glossary.md)
- [HTTP status code semantics](docs/backend/http-status-codes.md)

### Design analyses

- [Group-owned calendars and permissions](docs/backend/analysis/group-owned-calendars-and-permissions.md)
- [Integration testing strategy](docs/backend/analysis/integration-testing-strategy.md)
- [Child accounts and guardian/parent roles](docs/backend/analysis/child-accounts-and-guardian-roles.md)
- [Medicine schedules](docs/backend/analysis/medicine-schedules.md)
- [Meal plans](docs/backend/analysis/mealplans.md)

## Getting started

Backend:

```bash
cd src/backend/buddy
dotnet run
```

Frontend:

```bash
cd src/frontend/buddy
npm install
npm start
```

See [src/frontend/buddy/README.md](src/frontend/buddy/README.md) for
frontend-specific details (build, tests, and local app setup) and
[docs/backend/README.md](docs/backend/README.md) for backend documentation.
