# Buddy

Buddy is a scheduling and routine-support tool for parents and guardians of
children with ADHD. It gives families a shared calendar for events, tasks,
and daily routines, so a guardian can plan structure and support for a child
who benefits from clear, predictable schedules, while the child can see and
interact with their own day in a way that suits them.

## Why Buddy

Kids with ADHD often struggle less with knowing *what* to do than with
keeping track of *when* and *what's next*. Buddy is built around that
problem: shared calendars, recurring routines, and simple task/event
tracking that a parent or guardian sets up and a child can follow, with the
guardian able to see and adjust things as needed.

## Project structure

```
src/
  backend/   .NET API (event-sourced domain, Marten/PostgreSQL, Keycloak auth)
  frontend/  Angular application
docs/
  backend/   Backend documentation: glossary, user flow, HTTP status codes,
             and design analyses
```

- [Backend documentation](docs/backend/README.md) — glossary, users flow,
  and design analysis documents (including how groups/calendars share
  permissions, and how child accounts and guardian roles are modeled).
- [src/backend/buddy](src/backend/buddy) — the API itself.
- [src/frontend/buddy](src/frontend/buddy) — the Angular frontend.

## Core concepts

- **User** — an authenticated person (guardian or child), backed by
  Keycloak for authentication and modeled locally as an event-sourced
  aggregate with profile and email state. See the
  [glossary](docs/backend/glossary.md).
- **Calendar** — a scheduling container of events and tasks, owned by a
  user or a group, with per-member roles (`Owner`, `Contributor`, `Viewer`).
- **Group** — a collection of users (e.g. a family) with roles
  (`Owner`, `Admin`, `Member`) that can own calendars and control how those
  roles map to calendar permissions. See
  [Group-owned calendars and permissions](docs/backend/analysis/group-owned-calendars-and-permissions.md).
- **Guardian/child relationships** — how a parent or guardian is linked to
  a child's account and what that grants them, proposed in
  [Child accounts and guardian/parent roles](docs/backend/analysis/child-accounts-and-guardian-roles.md).

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
frontend-specific details (build, tests, etc.) and
[docs/backend/README.md](docs/backend/README.md) for backend documentation.
