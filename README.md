# Buddy

Buddy is a scheduling and routine-support tool for parents and guardians of
children with ADHD. It gives families a shared calendar for events, tasks,
and daily routines, so a guardian can plan structure and support for a child
who benefits from clear, predictable schedules, while the child can also see
and interact with their own day in a way that suits them.

## Why Buddy

Kids with ADHD often struggle less with knowing what to do than with keeping
track of when and what is next. Buddy is built around that problem: shared
calendars, recurring routines, and simple task and event tracking that a
parent or guardian sets up and a child can follow, with the guardian able to
see and adjust things as needed.

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
