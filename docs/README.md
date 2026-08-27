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
- [Pickups flow](backend/pickups/flow.md)
- [Glossary](backend/glossary.md)
- [HTTP status code semantics](backend/http-status-codes.md)
- [Testing](testing.md)

## Frontend

- [Frontend overview](frontend/README.md)
- [Installing Buddy on a kid's iPad](frontend/analysis/ipad-installation.md)
- [A single-day dashboard for the child home screen](frontend/analysis/child-day-dashboard.md)
- [Historical meal plans and children's ratings](frontend/analysis/mealplan-history-and-ratings.md)
- [Creating events across accessible calendars](frontend/analysis/calendar-agenda-and-event-creation.md)
- [Child calendar agenda](frontend/analysis/child-calendar-agenda-plan.md)
- [Guardian calendar day, work-week, week, and month views](frontend/analysis/guardian-full-calendar-views.md)
- [Child progress and rewards](frontend/analysis/child-progress-and-rewards.md)
- [Pickup planning and daily views](frontend/analysis/pickup-planning-and-daily-views.md)

## Backend design analysis

- [Aggregate roots and their relationships](backend/analysis/aggregate-roots.md)
- [Group-owned calendars and permissions](backend/analysis/group-owned-calendars-and-permissions.md)
- [All-day calendar items](backend/analysis/calendar-all-day-items.md)
- [Integration testing strategy](backend/analysis/integration-testing-strategy.md)
- [Mutation testing strategy](backend/analysis/mutation-testing-strategy.md)
- [Child accounts and guardian/parent roles](backend/analysis/child-accounts-and-guardian-roles.md)
- [Medicine schedules](backend/analysis/medicine-schedules.md)
- [Meal plans](backend/analysis/mealplans.md)
- [Group-shared meal plans](backend/analysis/group-owned-mealplans.md)
- [Meal plan iCal feed](backend/analysis/mealplan-ical-feed.md)
- [Pickup and drop-off schedules](backend/analysis/pickup-schedules.md)
- [Guardian-managed child language](backend/analysis/child-language-settings.md)
- [Gamified progress](backend/analysis/gamified-progress.md)

## Deployment

- [Deploying to Oracle Cloud](../deploy/README.md) — production stack setup
  with Docker, Keycloak, PostgreSQL, and Caddy on an Always Free VM.

## Scope

The documentation in this folder covers implemented backend and frontend
behavior, domain and authorization decisions, testing strategy, and product
analyses. App-specific development commands live in
[src/frontend/buddy/README.md](../src/frontend/buddy/README.md), while local
service setup is documented in the
[development container guide](../.devcontainer/README.md).
