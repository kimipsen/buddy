# A single-day dashboard for the child home screen

Status: Implemented

The child home at `/child` is a focused view of today rather than a calendar to
navigate. It combines planned meals, medicine doses, pickup/drop-off
assignments, and tasks in separate sections, then shows the child's guardian
and sibling relationships. This page preserves the design rationale and records
how the shipped dashboard evolved from it.

## Design outcome

The implemented screen follows the recommended “render each domain if it has
content” approach instead of merging unrelated items into one timeline.
Meals use named slots, medicine doses use exact local times, pickups use two
responsibility slots, and tasks use due times. Keeping those shapes separate
makes each action and state easier to scan.

A section with no rows is omitted. Only when meals, doses, pickups, and tasks
are all empty does the page show the single nothing-to-show state. This avoids
stacking four empty cards on a quiet day.

## Data loading

[`ChildHome`](../../../src/frontend/buddy/src/app/features/child/home/home.ts)
resolves the signed-in user through `UsersService.ensureCurrentUser()` and
loads today's domain data from existing services:

- `MealplansService.listMealPlan()` for the child's family plan;
- `MedicinesService.listDoses()` for today's dose occurrences;
- `PickupsService.listSchedule()` for today's pickup/drop-off assignments;
- `CalendarsService.listTodayOccurrences()`, split into tasks and events;
- `GuardiansService.listMyGuardians()` and `listMySiblings()` for relationship
  summaries and pickup assignee names.

These requests do not require a child picker because the route always acts as
the authenticated child. The sections maintain independent signals, while a
shared translated error state reports failed actions or dashboard loading.

## Meals and ratings

Planned meals are displayed in breakfast, lunch, dinner, and snack order. Empty
slots are skipped rather than rendered as “not planned” filler.

The original analysis treated ratings as a separate future flow. The shipped
home screen supports them directly: tapping a star submits the rating
immediately while preserving any existing comment, and a separate comment
editor lets the child update text. A successful response updates every visible
slot that references the same meal. The full current/history workflow also
exists at `/child/mealplan`.

## Medicine doses

Doses are sorted by time and show `Pending`, `Taken`, or `Skipped`. The child can
change their own dose status through `MedicinesService.setDoseStatus()`. The row
is updated from the returned occurrence and disabled while that dose request is
active.

Medicine remains a separate domain from calendar items. The dashboard composes
the results at presentation time rather than projecting doses into a calendar.

## Pickup and drop-off

Today's assigned `DropOff` and `PickUp` occurrences are read-only for the
child. Guardian and sibling IDs are resolved against the relationship lists;
self-escort and playdate use their own display variants. Missing occurrences do
not render a placeholder row.

The guardian editing workflow is documented in
[Pickup planning and daily views](pickup-planning-and-daily-views.md).

## Tasks

Calendar occurrences are filtered to tasks and sorted by due time, with undated
tasks last. Task completion is implemented: toggling a row calls
`CalendarsService.setTaskCompletion()` for today's occurrence and updates the
local result after the server succeeds.

This closes the blocker recorded in the original analysis. `CalendarItem` now
has a per-occurrence completion operation in the backend and the child can use
it through the existing calendar authorization model.

## Events

Calendar occurrences are also filtered to events (the complement of the Tasks
filter above) and sorted by start time, with all-day/undated events last. This
reuses the same `listTodayOccurrences()` call already made for tasks — no
extra request. Events are read-only here (no completion concept applies to an
event); `ChildVisibility.FilterForChild` on the backend already returns every
event on an accessible calendar, unlike tasks which are trimmed to the
child's own assignments.

Each event is also given a derived view state, recomputed once a minute
against the current time: past events (already ended) are shown struck
through with a checkmark, and ongoing events (started but not yet ended) get
a background gradient that fills left-to-right as the event progresses,
computed from elapsed time over the event's duration. All-day/undated events
have no start/end to measure against and are never marked past or ongoing.
[`EventsToday`](../../../src/frontend/buddy/src/app/features/guardian/events-today/events-today.ts)
on the guardian dashboard applies the same past/ongoing/progress derivation to
its list of today's events.

This section is the "today" half of showing the child their calendars. The
multi-day browsing half is implemented separately at `/child/calendar` — see
[Child calendar agenda implementation plan](child-calendar-agenda-plan.md).

## Interaction and state

Each mutation has a narrow saving key (`MealSlot`, medicine/date/time key, or
calendar item ID), so unrelated controls remain usable while a request is in
flight. Failed operations leave server-backed data unchanged and surface the
translated error state.

Static copy is translated in English and Danish. Domain times are local
wall-clock values and use the application's locale-aware display conventions.

## Deliberate boundaries

The dashboard remains today-only:

- no previous/next day navigation;
- no merged chronological timeline;
- no event creation or full child calendar agenda;
- no pickup editing by the child;
- no per-section empty-state cards.

Historical meal browsing has its own route, and guardian calendar/pickup editing
have their own routes. A full multi-day calendar agenda for children now
exists at `/child/calendar` — see [Child calendar agenda implementation
plan](child-calendar-agenda-plan.md) and the broader context in
[Creating events and seeing them across every accessible calendar](calendar-agenda-and-event-creation.md).
