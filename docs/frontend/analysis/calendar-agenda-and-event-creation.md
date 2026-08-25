# Creating events and seeing them across every accessible calendar

The ask: a user should be able to create events in the calendars they have access to, and see
events from all calendars they have access to — whether that access is their own, or comes
through a group.

Status: the guardian-facing agenda and creation UI described below is now implemented
(`features/guardian/calendar/`, routed at `/guardian/calendar`, linked from the profile menu).
The child-facing side remains the deliberately deferred open question noted below — nothing
child-facing changed.

## Headline finding: this is a UI gap, not a backend one

Every piece of authorization and data-shape work this ask requires already exists and is already
correct server-side. There is no new endpoint, event type, aggregate, or authorization rule needed
to ship what's being asked for — the entire remaining task is building the screens.

- **"Events in the calendars I have access to" is already one unified access model.**
  [`CalendarAuthorization.ResolveRole`](../../../src/backend/buddy/Features/Calendars/CalendarAuthorization.cs#L87-L131)
  resolves a caller's role on a calendar through three paths, in order: an explicit
  `Calendar.Members` grant (always wins), else — if the calendar is group-owned — the caller's
  `GroupRole` mapped through `Group.CalendarPermissionPolicy`, else — if the calendar is
  user-owned and the caller isn't the owner — an active `GuardianLink` to the owner grants
  `CalendarRole.Owner` implicitly. `CreateItem` is gated on this via `CheckContribute`
  ([`CreateItem.Handler.cs`](../../../src/backend/buddy/Features/Calendars/CreateItem/CreateItem.Handler.cs#L30)),
  so "create an event in a calendar I have access to" already means the same thing for a personal
  calendar and a group-owned one.
- **"See events from all calendars I have access to (own or via group)" is already answered by
  one query.**
  [`ListCalendars.Handler`](../../../src/backend/buddy/Features/Calendars/ListCalendars/ListCalendars.Handler.cs)
  merges three sources unconditionally: explicit `CalendarMembershipDocument` rows, group-derived
  calendars (the caller's `GroupRole` in each group they belong to, mapped through that group's
  `CalendarPermissionPolicy`, joined against `GroupOwnedCalendarDocument`), and guardian-derived
  calendars (every linked child's own calendars). The same explicit-wins precedence rule from
  single-calendar resolution is applied when a calendar shows up in more than one source.
- **Occurrences already support arbitrary date ranges.**
  [`ListOccurrences`](../../../src/backend/buddy/Features/Calendars/ListOccurrences) takes
  `from`/`to` and recomputes recurrence expansion for that window — nothing restricts it to
  "today," that's purely how the frontend happens to call it today (see below).
- **A calendar is always group-owned at creation now.** `CreateCalendar` requires a `GroupId` and
  is gated on `GroupAuthorization.CheckManage` on that group
  ([`CreateCalendar.Command.cs`](../../../src/backend/buddy/Features/Calendars/CreateCalendar/CreateCalendar.Command.cs)) —
  the plain user-owned `CalendarCreated` event is kept readable forever for calendars made before
  this change, but no UI needs to offer a "personal calendar" creation path going forward.

The permission design itself is written up in full in
[group-owned-calendars-and-permissions.md](../../backend/analysis/group-owned-calendars-and-permissions.md);
that document's status line said "Proposed" but everything in it is shipped — see the correction
alongside this document.

## What the frontend actually has today

- [`manage-calendars`](../../../src/frontend/buddy/src/app/features/guardian/admin/manage-calendars/manage-calendars.ts)
  (under `/guardian/admin`) creates a group-owned calendar and moves an existing calendar to a
  different group. It never touches calendar *items* — no event, no task, nothing inside a
  calendar is ever created, viewed, or edited from this screen.
- [`CalendarsService`](../../../src/frontend/buddy/src/app/core/calendars.service.ts) has no
  `createItem`/`updateItem`/`deleteItem` method at all, even though the backend slices
  (`CreateItem`, `UpdateItemDetails`, `UpdateItemRecurrence`, `RescheduleItem`, `DeleteItem`) are
  fully built. The only item-level call it exposes is `setTaskCompletion` (ticking a task done).
- The only consumers of calendar data are three "today" widgets —
  [`events-today`](../../../src/frontend/buddy/src/app/features/guardian/events-today/events-today.ts),
  [`tasks-today`](../../../src/frontend/buddy/src/app/features/guardian/tasks-today/tasks-today.ts),
  and the task list inside
  [child `home.ts`](../../../src/frontend/buddy/src/app/features/child/home/home.ts) — all reading
  from `CalendarsService.listTodayOccurrences()`, which fans out `listMyCalendars()` into one
  `listOccurrences(calendarId, today, today)` per calendar and flattens the result, memoized for
  the current day only.
- No route exists for a calendar view at all: `GUARDIAN_ROUTES`
  ([`guardian.routes.ts`](../../../src/frontend/buddy/src/app/features/guardian/guardian.routes.ts))
  has `mealplan`, `medicine`, `pickup`, `admin`; `CHILD_ROUTES`
  ([`child.routes.ts`](../../../src/frontend/buddy/src/app/features/child/child.routes.ts)) has only
  `mealplan`. There is no `/guardian/calendar` or `/child/calendar`.

## Two independent problems

Same shape as the meal-plan history gap: the ask splits into two pieces that are each
independently shippable, and both are needed for the full ask to be true end-to-end.

1. **Creating an event or task has no UI anywhere**, on either the guardian or child side, for any
   calendar — personal or group-owned.
2. **Seeing events is currently limited to "today," flattened and unlabeled.** A user can't look
   at any other date, and even for today there's no way to tell *which* calendar (their own vs.
   which group's) a given occurrence came from, or to hide one calendar's events selectively.

## Problem 1: event/task creation

Needs, all additive, no backend change:

- **Service methods** on `CalendarsService`: a `createItem` wrapping
  `POST /calendars/{calendarId}/items` with the same shape `CreateItem.Command` already accepts
  (`kind`, `title`, `icon`, `color`, and either `startsAt`/`endsAt` for an event or `dueAt` for a
  task, plus an optional recurrence rule) — a thin wrapper in the same style as `createCalendar`.
- **A calendar picker restricted to calendars the user can actually contribute to.**
  `CalendarSummary.role` is already returned by `listMyCalendars()` with the same numeric
  ordinals `CalendarAuthorization` uses (`0 = Owner`, `1 = Contributor`, `2 = Viewer`) — filtering
  to `role <= 1` client-side is enough; no new endpoint or response field is needed to know which
  calendars a "create event" form should even offer.
- **A create form**: title, icon/color pickers (the same `Icon`/`Color` value types
  [`Medicines`](../../backend/analysis/medicine-schedules.md) already reuses from `Calendars` for
  visual consistency — check for an existing icon/color picker component before building a new
  one), event-vs-task toggle, start/end or due date+time, and an optional recurrence rule editor
  (`Daily | Weekly | Monthly | Yearly`, interval count, optional end date).
- **Where it lives**: a new `features/guardian/calendar/` area (this document doesn't resolve
  whether a `features/child/calendar/` counterpart should exist too — see open questions).

One thing worth flagging rather than assuming: `CheckContribute` is symmetric — anyone who
resolves to `Owner` or `Contributor` on a calendar can create items on it, with no separate
guardian/child tiering the way `Medicines` has a Manage/Mark split. If a child were ever granted
`Contributor` on their own calendar, they could already create events there today, purely because
of how that calendar's roles are set — this is the existing, deliberate `Calendar` permission
model, not something the new UI should try to route around or that this document is proposing to
change.

## Problem 2: an agenda view across every accessible calendar

Needs, all additive, no backend change:

- **A date-range fetch, not just "today."** `fetchTodayOccurrences`
  ([`calendars.service.ts`](../../../src/frontend/buddy/src/app/core/calendars.service.ts)) already
  has exactly the right shape — fan out `listMyCalendars()` into one `listOccurrences` call per
  calendar and flatten — it's just hardcoded to `[today, today]` and memoized as a same-day cache
  for the two dashboard widgets. The agenda view needs a second, separate method parameterized by
  an arbitrary `[from, to]` range, not a change to `listTodayOccurrences` itself — conflating an
  on-demand range fetch with the dashboard widgets' per-day cache would be the wrong shape for
  both callers.
- **Attributing each occurrence to a calendar the user can recognize.** `CalendarOccurrence`
  already carries `calendarId`, but nothing today joins that back to the calendar's `name` (or a
  color) for display — the range-fetch method should attach `CalendarSummary.name`/color the same
  way `fetchTodayOccurrences` already attaches `calendarId` onto each occurrence.
- **A real agenda screen** — month/week/day view (product choice, not resolved here), plus a
  per-calendar show/hide toggle, which is pure client-side filtering once occurrences carry
  `calendarId`.
- **Routing**: add a `calendar` child route to `GUARDIAN_ROUTES` (and, if in scope, `CHILD_ROUTES`).

## What doesn't change

No new backend endpoint, event type, aggregate, or authorization rule. The explicit-grant-wins /
group-policy / guardian-link precedence in `CalendarAuthorization.ResolveRole` and the three-source
merge in `ListCalendars.Handler` already implement exactly "own or via group" as asked — this
document's scope is entirely the two frontend features above plus the routing/i18n plumbing they
need (new translation keys under a `calendar` domain, following the
`translations/{en,da}/<domain>.ts` convention already used for `admin.manageCalendars.*`).

## Recommendation

Ship in this order:

1. **Date-range agenda fetch + a real agenda screen** — cheapest (reuses
   `fetchTodayOccurrences`'s existing shape almost unchanged) and immediately makes "see events
   from all my calendars" true for any date, not just today.
2. **Event/task creation form + `createItem` service method** — the larger piece, since right now
   this backend capability (`CreateItem`, fully built and authorized) has zero frontend consumers
   at all.
3. **Calendar-source polish** — per-calendar color/name labeling and show/hide filtering in the
   agenda view — do this once (1) exists and it's clear what a multi-calendar agenda actually
   needs to distinguish, rather than guessing the visual treatment before there's a screen to hang
   it on.

## Open questions

- **Should children create events themselves?** Nothing server-side blocks it for a calendar
  they're a `Contributor`/`Owner` on, but no such calendar-role grant to a child exists in any
  flow today. Decide whether `features/child/calendar/` gets a create form or stays read-only —
  this is a product decision, not a backend gate.
- **Should the agenda view show all accessible calendars merged into one flat timeline, or group
  them (personal vs. each group) as visually distinct sections?** The backend gives no preference
  either way — `ListCalendars` just returns a flat set.
- **Should this new calendar area absorb `manage-calendars` (admin), or stay separate from it?**
  Today "create a calendar" (admin, rare) and "create an event in a calendar" (day-to-day) are
  different concerns; merging them into one `features/guardian/calendar/` area is plausible but
  not required by anything in this document.
