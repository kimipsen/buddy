# Backend

Backend documentation for the Buddy API.

## Start here

- [Users flow](users/flow.md)
- [Glossary](glossary.md)
- [HTTP status code semantics](http-status-codes.md)

## Design analysis and decision records

These documents capture the domain decisions that are easy to lose during
implementation and are therefore worth reading before changing the backend
model or permissions logic.

- [Group-owned calendars and permissions](analysis/group-owned-calendars-and-permissions.md)
- [Integration testing strategy](analysis/integration-testing-strategy.md)
- [Child accounts and guardian/parent roles](analysis/child-accounts-and-guardian-roles.md)
- [Medicine schedules](analysis/medicine-schedules.md)

## Current focus areas

The backend is centered on a few core concepts: user identity and guardian
relationships, shared calendar permissions, event-sourced aggregates, and the
proposed medicine schedule feature for child routines. If you are making a
behavioral or authorization change, check the relevant analysis document before
changing the implementation.
