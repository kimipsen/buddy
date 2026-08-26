# Child calendar agenda (`/child/calendar`) — implementation plan

Status: Planned, not started. This is the second phase of "show the child
their calendars"; phase one (today's events on the dashboard) is implemented
and documented in [A single-day dashboard for the child home
screen](child-day-dashboard.md).

## Goal

Give the child a way to browse more than "today" — a multi-day agenda of the
calendars they already have access to, read-only. This does not change what a
child can see (no new backend authorization), only how much of it they can
browse at once.

## Scope decision (already made)

Frontend-only. Children keep exactly the calendar access they have today
(explicit `Calendar.Members` grant, or membership in a group that owns a
calendar — see
[`CalendarAuthorization.ResolveRole`](../../../src/backend/buddy/Features/Calendars/CalendarAuthorization.cs)).
No new "child sees guardian's personal calendars automatically" rule is part
of this phase. If that broader access model is wanted later, it is a separate,
security-sensitive backend change and should get its own design pass (see
"Explicitly out of scope" below).

## Open product decisions to confirm before starting

These are called out as unresolved in [Creating events and seeing them across
every accessible calendar](calendar-agenda-and-event-creation.md#what-remains-deferred)
and should be settled (even briefly, with the requester) before writing code:

1. **Browse only, or also create/edit?** The backend already supports a child
   holding `Contributor`/`Owner` on a calendar, so item creation is not
   blocked technically — but whether a child should be able to create events
   is a product call, not an engineering one. Default recommendation: **view
   + task completion only**, matching what the dashboard already allows.
2. **Single merged agenda, or per-calendar view?** Recommendation: mirror the
   guardian agenda — merge all accessible calendars into one view with a
   show/hide filter per calendar, since that's already a proven, tested
   pattern.
3. **How far back/forward should navigation go?** The guardian agenda shows a
   rolling 7-day window from an adjustable anchor date
   (`DAYS_AHEAD = 7` in `agenda.ts`). Recommendation: reuse the same window
   unchanged rather than inventing new pagination.

## Backend

No changes required. Confirmed already sufficient:

- `GET /calendars` (`CalendarsService.listMyCalendars()`) — calendars visible
  to the caller, each with an effective role.
- `GET /calendars/{id}/occurrences?from&to` (`CalendarsService.listOccurrences()`,
  wrapped by `listOccurrencesInRange()`) — event/task occurrences for a date
  range, calendar-tagged.
- `ChildVisibility.FilterForChild` (`src/backend/buddy/Features/Calendars/ChildVisibility.cs`)
  is already applied server-side inside `ListOccurrences.Handler` — the child
  will see all events but only their own assigned tasks, same as the
  dashboard today. No client-side filtering needed or should be added.
- `PATCH /calendars/{id}/items/{itemId}/completion` (`setTaskCompletion()`) —
  already used by the dashboard for task toggling, reusable as-is if decision
  #1 above keeps task completion in scope.

## Frontend plan

### 1. Route

Add `'calendar'` to
[`child.routes.ts`](../../../src/frontend/buddy/src/app/features/child/child.routes.ts),
lazy-loaded, next to the existing `mealplan` route. Same pattern, no route
guard changes needed (child/guardian routing is already resolved by
`role.guard.ts` before either route tree loads).

### 2. Component — adapt, don't reuse, the guardian agenda

Reference:
[`CalendarAgenda`](../../../src/frontend/buddy/src/app/features/guardian/calendar/agenda/agenda.ts)
(`/guardian/calendar`, 511 lines) and its template
[`agenda.html`](../../../src/frontend/buddy/src/app/features/guardian/calendar/agenda/agenda.html)
(429 lines).

Build a new `features/child/calendar/child-calendar.ts` / `.html` (do not
import the guardian component directly — it's guardian-routed and carries
write-path state that a read-only child view shouldn't drag in). Port over:

- `buildDays` / `parseIsoDate` / `addDaysIso` / `instantFor` helpers
  (agenda.ts:37-67) — pure date-window logic, copy as-is.
- `anchorDate` / `days` signals and previous/next navigation (agenda.ts:86-87)
  — unchanged.
- `myCalendars`, `occurrences`, `occurrencesByDate`, `hasAnyVisibleOccurrence`,
  `hiddenCalendarIds` signals and the per-calendar filter UI — unchanged; a
  child merging multiple calendars needs the same filter a guardian does.
- `savingTaskId` + `toggleTask`-equivalent — keep **only if** decision #1
  above keeps task completion in scope; the dashboard's `ChildHome.toggleTask()`
  (`home.ts`) is the simpler reference implementation for that half (it
  doesn't need the guardian's broader edit-item plumbing).

Explicitly drop from the port:

- `eligibleCalendars` / `MAX_CONTRIBUTE_ROLE` and everything under the
  "editing"/"creating" signals (`editingItemId`, `editTitle`, `newCalendarId`,
  `canSubmitEdit`, etc., agenda.ts:100-160+) — no create/edit form.
  `createItem`, `updateItemDetails`, `updateItemRecurrence`, `rescheduleItem`,
  `deleteItem` calls — none of these should be reachable from the child view
  under decision #1's default.
  `confirmingDeleteItemId` / `deletingItemId` and delete confirmation UI.
- `FormsModule`, `DateSelect`, `TimeSelect` imports — only needed for the
  create/edit form being dropped.

### 3. Entry point

Add a link/card on `ChildHome` (`home.html`), styled like the existing
`routerLink="/child/mealplan"` link (home.html:20-26), pointing at
`/child/calendar`. Suggested placement: near the new "Events today" section
added in phase 1, since it's the natural "see more" affordance for that
section.

### 4. i18n

New keys under `child.calendar.*` in both
[`en/child.ts`](../../../src/frontend/buddy/src/app/core/i18n/translations/en/child.ts)
and
[`da/child.ts`](../../../src/frontend/buddy/src/app/core/i18n/translations/da/child.ts),
following the `child.mealplan.*` naming precedent already in those files
(`back`, `title`, `previousWeek`/`nextWeek`, `loading`, `loadError`,
`emptyTitle`). Reuse `calendar.agenda.allDay` key/copy from the guardian
translations where the shape matches, rather than inventing new copy for the
same concept.

### 5. Testing

Follow `agenda.spec.ts`'s pattern for date-window and occurrence-grouping
tests, but scoped to what actually ships:

- day navigation renders the right date range;
- occurrences group onto the correct day, respecting the child's time zone
  (`UsersService.timeZoneId()` — same stub pattern as `agenda.spec.ts` and the
  phase 1 test in `home.spec.ts`);
- per-calendar hide/show filtering;
- (if task completion is kept) toggling a task calls
  `CalendarsService.setTaskCompletion()`, mirroring the existing
  `home.spec.ts` "toggles a task's completion" test;
- no create/edit/delete affordance is rendered anywhere in the child view —
  worth an explicit negative assertion given how easy it'd be to accidentally
  carry a stray button over from a copy-paste of `agenda.html`.

## Explicitly out of scope for this phase

- Any `CalendarAuthorization.ResolveRole` change (i.e. children automatically
  seeing a guardian's personal calendars they aren't already a member of).
  If wanted later, treat it as its own backend design task — see the
  "Expand backend access to guardian calendars" option that was deferred when
  scoping phase 1.
- Event/task creation, editing, rescheduling, recurrence editing, or deletion
  by the child, unless decision #1 above is explicitly revisited.
- A restricted/scoped child auth token — not needed; the existing
  business-logic authorization (`ChildVisibility`, `CalendarAuthorization`)
  already gates everything server-side regardless of frontend routes reached.
