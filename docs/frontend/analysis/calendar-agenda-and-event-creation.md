# Creating events and seeing them across every accessible calendar

Status: Guardian workflow implemented; child calendar access remains deferred.

The original requirement was to let a user create events in calendars they can
contribute to and browse occurrences from every calendar they can access,
whether access is explicit, guardian-derived, or group-derived. The backend
already provided that authorization and occurrence expansion. The implemented
frontend now exposes it to guardians at `/guardian/calendar`.

## Backend contract reused by the UI

The frontend did not need a new aggregate or permission rule:

- [`CalendarAuthorization.ResolveRole`](../../../src/backend/buddy/Features/Calendars/CalendarAuthorization.cs)
  resolves explicit membership first, then a group-owned calendar's permission
  policy, then guardian-derived access to a user-owned calendar.
- [`ListCalendars.Handler`](../../../src/backend/buddy/Features/Calendars/ListCalendars/ListCalendars.Handler.cs)
  merges explicit, group-derived, and guardian-derived calendars and returns
  the effective role for each.
- `ListOccurrences` expands events and tasks for an arbitrary date range.
- Item commands already provide create, detail update, rescheduling,
  recurrence update, task completion, and deletion operations.

The full ownership and precedence rules remain documented in
[Group-owned calendars and permissions](../../backend/analysis/group-owned-calendars-and-permissions.md).

## Implemented guardian workflow

[`GuardianCalendar`](../../../src/frontend/buddy/src/app/features/guardian/calendar/calendar.ts)
hosts the agenda at `/guardian/calendar`. Its agenda component loads accessible
calendars, fetches a week of occurrences from each selected calendar, and
retains calendar identity so entries can be attributed and filtered.

The screen supports:

- previous/next week navigation;
- merged event and task occurrences from accessible calendars;
- per-calendar show/hide filtering;
- event/task creation in calendars where the effective role can contribute;
- title, icon, color, all-day, date/time, and task due-date fields;
- daily, weekly, monthly, and yearly recurrence with interval and optional end
  date;
- editing details, schedule, and recurrence through the corresponding backend
  operations;
- task completion from the agenda;
- deletion with confirmation.

Calendar administration remains separate under `/guardian/admin`. Creating or
moving a calendar is an administrative workflow; creating an item inside an
existing calendar is a day-to-day agenda workflow.

## Service and data flow

[`CalendarsService`](../../../src/frontend/buddy/src/app/core/calendars.service.ts)
now exposes the item operations that were missing when this analysis was first
written: `createItem`, `updateItemDetails`, `updateItemRecurrence`,
`rescheduleItem`, `deleteItem`, and `setTaskCompletion`.

The agenda does not reuse the dashboard's today-only cache. It requests the
visible date range, fans out occurrence requests across accessible calendars,
and joins results with calendar summaries for names and filtering. This keeps
the dashboard's cheap current-day path independent from agenda navigation.

All-day dates use the backend's inclusive-start/exclusive-end representation.
The UI converts between that representation and the inclusive dates a user
selects, preserving the semantics documented in
[All-day calendar items](../../backend/analysis/calendar-all-day-items.md).

## Authorization behavior

The calendar picker only offers calendars whose returned role can contribute.
This is an ergonomic filter, not the security boundary: backend handlers still
resolve the caller's current role for every write. Viewer calendars remain
visible in the merged agenda but cannot be selected as write targets.

Explicit grants continue to override group-derived policy. A calendar can
therefore appear once even if the user reaches it through multiple access
paths, with the backend-provided effective role controlling the UI.

## What remains deferred

There is no `/child/calendar` route. The child home shows today's tasks and can
toggle task completion, but children do not get the guardian's week agenda or
item creation form.

That remains a product decision rather than a missing backend primitive. A
child with a contributor-capable calendar role could already be authorized by
the calendar model, but the product must decide:

- whether children should browse a full agenda or retain the focused daily
  dashboard;
- whether they may create/edit items or only view and complete tasks;
- how calendar membership should be granted to a child in normal family setup;
- whether a child agenda should merge calendars or emphasize one personal
  schedule.

The current implementation deliberately resolves the guardian requirement
without pre-empting those child-experience decisions.
