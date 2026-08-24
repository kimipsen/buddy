# Kim Documentation

This folder contains the project-level documentation for Buddy.

## Start here

- [Backend overview](backend/README.md)
- [Users flow](backend/users/flow.md)
- [Calendars flow](backend/calendars/flow.md)
- [Groups flow](backend/groups/flow.md)
- [Guardians flow](backend/guardians/flow.md)
- [Medicines flow](backend/medicines/flow.md)
- [Mealplans flow](backend/mealplans/flow.md)
- [Glossary](backend/glossary.md)
- [HTTP status code semantics](backend/http-status-codes.md)

## Frontend

- [Frontend overview](frontend/README.md)
- [Installing Buddy on a kid's iPad](frontend/analysis/ipad-installation.md)

## Backend design analysis

- [Group-owned calendars and permissions](backend/analysis/group-owned-calendars-and-permissions.md)
- [Integration testing strategy](backend/analysis/integration-testing-strategy.md)
- [Mutation testing strategy](backend/analysis/mutation-testing-strategy.md)
- [Child accounts and guardian/parent roles](backend/analysis/child-accounts-and-guardian-roles.md)
- [Medicine schedules](backend/analysis/medicine-schedules.md)
- [Meal plans](backend/analysis/mealplans.md)
- [Group-shared meal plans](backend/analysis/group-owned-mealplans.md)
- [Meal plans](backend/analysis/mealplans.md)

## Deployment

- [Deploying to Oracle Cloud](../deploy/README.md) — production stack setup
  with Docker, Keycloak, PostgreSQL, and Caddy on an Always Free VM.

## Scope

The documentation in this folder focuses on the backend domain model,
authorization rules, lifecycle decisions, and the planned features that shape
Buddy's behavior. The Angular app has its own usage and setup guidance in
[src/frontend/buddy/README.md](../src/frontend/buddy/README.md).
