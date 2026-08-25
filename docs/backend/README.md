# Backend

Backend documentation for the Buddy API.

## Start here

- [Users flow](users/flow.md)
- [Calendars flow](calendars/flow.md)
- [Groups flow](groups/flow.md)
- [Guardians flow](guardians/flow.md)
- [Medicines flow](medicines/flow.md)
- [Mealplans flow](mealplans/flow.md)
- [Glossary](glossary.md)
- [HTTP status code semantics](http-status-codes.md)

## Design analysis and decision records

These documents capture the domain decisions that are easy to lose during
implementation and are therefore worth reading before changing the backend
model or permissions logic.

- [Aggregate roots and their relationships](analysis/aggregate-roots.md)
- [Group-owned calendars and permissions](analysis/group-owned-calendars-and-permissions.md)
- [Integration testing strategy](analysis/integration-testing-strategy.md)
- [Mutation testing strategy](analysis/mutation-testing-strategy.md)
- [Child accounts and guardian/parent roles](analysis/child-accounts-and-guardian-roles.md)
- [Medicine schedules](analysis/medicine-schedules.md)
- [Meal plans](analysis/mealplans.md)
- [Group-shared meal plans](analysis/group-owned-mealplans.md)
- [Pickup and drop-off schedules](analysis/pickup-schedules.md)
- [Guardian-managed child language](analysis/child-language-settings.md)

## Current focus areas

The backend is centered on core concepts including:

- user identity and guardian relationships
- shared calendar permissions
- event-sourced aggregates
- medicine schedules for child routines
- meal planning and family meal libraries

If you are making a behavioral or authorization change, check the relevant
analysis document or flow doc before changing the implementation.
