# All-Day Calendar Items

Status: Implemented

## Context

Every calendar item today carries a mandatory time-of-day. An Event's schedule is a
[`Period`](../../../src/backend/buddy/Features/Calendars/Types/Period.cs)
(`StartsAt`/`EndsAt`, each a `DateOnly` + `TimeOnly`), and a Task's schedule is a single
[`DueDate`](../../../src/backend/buddy/Features/Calendars/Types/DueDate.cs)
(`DateOnly` + `TimeOnly`). The agenda create/edit forms
([`agenda.ts`](../../../src/frontend/buddy/src/app/features/guardian/calendar/agenda/agenda.ts),
[`agenda.html`](../../../src/frontend/buddy/src/app/features/guardian/calendar/agenda/agenda.html))
always pair an `app-date-select` with an `app-time-select`, so there is no way to schedule
something for "just a day" — a birthday, a school holiday, a full-day trip — without picking an
arbitrary time that has no real meaning.

The ask: let both Events and Tasks be marked **all-day**, so no start/end (Event) or due (Task)
time has to be chosen.

## Decision: an explicit `IsAllDay` flag, not an inferred one

Two ways to model "no time":

1. **Explicit flag**, persisted on the schedule value objects (`Period`, `DueDate`).
2. **Inferred from a sentinel time** — e.g. treat `Time == 00:00` as "all-day," with no new field.

**Decision: explicit flag.** A sentinel can't be told apart from a real item that genuinely starts
at midnight (a New Year's countdown event, a task due at the start of the day) — that ambiguity
would corrupt both what's rendered ("All day" vs. "00:00") and what the UI restores into the
all-day checkbox when editing. An explicit `IsAllDay` boolean has no such collision and round-trips
through create → reschedule → edit without guessing.

Scope: **both kinds** get the flag — Event (`Period.IsAllDay`) and Task (`DueDate.IsAllDay`) — not
just one. A Task's single due time and an Event's start/end pair are different shapes, but "the
time-of-day doesn't matter for this item" is the same concept for both, and the create/edit form
already branches on kind for every other field.

## Domain model changes

```
Period(StartsAt, EndsAt, bool IsAllDay)
DueDate(DateOnly Date, TimeOnly Time, bool IsAllDay = false)
```

- [`Period.cs`](../../../src/backend/buddy/Features/Calendars/Types/Period.cs): `IsAllDay` becomes
  a third property, threaded through `TryCreate(StartsAt, EndsAt, bool isAllDay)` and the private
  `JsonConstructor` ctor. `TryCreate`'s existing validation (`EndsAt > StartsAt`) is untouched — see
  "Multi-day events" below for why it still holds for all-day periods.
- [`DueDate.cs`](../../../src/backend/buddy/Features/Calendars/Types/DueDate.cs): `IsAllDay` is
  added as a third positional parameter with a `= false` default.

**Backward compatibility.** Both types are read back from Marten's event store via
`System.Text.Json`, using the record's primary constructor
([`MartenCalendarItemEventStore.cs`](../../../src/backend/buddy/Features/Calendars/MartenCalendarItemEventStore.cs)
rehydrates every `CalendarItemEvent` this way). A constructor parameter with a default value is
treated as optional by `System.Text.Json`'s parameterized-constructor deserialization, so any
`Period`/`DueDate` JSON written before this change — with no `IsAllDay` property at all — still
deserializes cleanly, filling in `false`. No upcasting, no backfill, no migration script.

**Rejected alternative: a separate `IsAllDay` field on `CalendarItem`/the events, instead of on
`Period`/`DueDate` themselves.** Both value objects are what's actually persisted inside
`EventItemCreated.Period`, `TaskItemCreated.DueDate`, `EventRescheduled.Before`/`After`, and
`TaskRescheduled.Before`/`After`
([`CalendarItemEvents.cs`](../../../src/backend/buddy/Features/Calendars/Types/CalendarItemEvents.cs)).
Putting the flag on a sibling field would require every event and every place that reads or writes
a schedule to separately keep the flag in sync with the dates/times it describes — two facts that
must never disagree, expressed as two independent fields. Putting it inside the value object makes
"all-day" and "the actual schedule" one indivisible fact, and it flows through `CalendarItem.Rehydrate`
([`CalendarItem.cs`](../../../src/backend/buddy/Features/Calendars/Types/CalendarItem.cs)) and the
JSON-returned `CalendarItemResponse` for free.

## Multi-day all-day events: inclusive UI, exclusive storage

An all-day Event can span more than one day (a trip, a school break). `Period.TryCreate` requires
`EndsAt > StartsAt`, and `AddEventOccurrences`
([`CalendarOccurrenceExpansion.cs`](../../../src/backend/buddy/Features/Calendars/CalendarOccurrenceExpansion.cs))
computes a duration as `EndsAt - StartsAt` — both already assume `EndsAt` is a point in time
strictly after `StartsAt`, not an inclusive calendar day.

**Decision:** keep `EndsAt` exclusive at the domain/storage layer (same as today — this needs no
handler change), but let the *UI* collect an **inclusive** end date and convert it at the boundary:

| Layer | "Jun 1 to Jun 3" (3-day trip) means |
|---|---|
| User-facing (`app-date-select`, both create and edit forms) | Start date `Jun 1`, end date `Jun 3` — the last day of the trip, as a person would say it |
| `RescheduleItemRequest`/`CreateItemRequest` sent to the API | `startsAt = {Jun 1, 00:00}`, `endsAt = {Jun 4, 00:00}` — one day past the last all-day day |
| `Period.TryCreate` validation | `EndsAt (Jun 4 00:00) > StartsAt (Jun 1 00:00)` — holds structurally, same rule as today |
| Occurrence duration (`AddEventOccurrences`) | `3 days` exactly, unchanged formula |

This mirrors RFC 5545's own convention for a `DATE`-valued `DTEND` (exclusive), so it also lines up
with what the iCal feed needs to emit (see below) instead of fighting it.

A single-day all-day event is the `N=1` case of the same rule: UI start = end = the one day,
`EndsAt` sent as start date + 1 day.

## Occurrence expansion and the flattened `CalendarItemOccurrence`

[`CalendarItemOccurrence`](../../../src/backend/buddy/Features/Calendars/Types/CalendarItemOccurrence.cs)
is a computed, never-persisted projection — `StartsAt`/`EndsAt`/`DueAt` are already-resolved
`DateTimeOffset?` instants, not the `Period`/`DueDate` value objects. It needs its own `IsAllDay`
property, since the instants alone can't answer "was this all-day" (an instant of exactly midnight
is indistinguishable from a real midnight-start event, the same ambiguity the flag exists to avoid).
`AddEventOccurrences`/`AddTaskOccurrences` set it from `item.Period!.IsAllDay` /
`item.DueDate!.IsAllDay` — no new lookup, the source item already carries it.

## Commands, handlers, and the API surface

- [`CreateItem.Command.cs`](../../../src/backend/buddy/Features/Calendars/CreateItem/CreateItem.Command.cs)
  and
  [`RescheduleItem.Command.cs`](../../../src/backend/buddy/Features/Calendars/RescheduleItem/RescheduleItem.Command.cs)
  (plus their `FromClaims` factories and `...Request` DTOs in the matching `Endpoint.cs` files) gain
  a `bool IsAllDay` parameter.
- `CreateItemHandler`/`RescheduleItemHandler` pass it into `Period.TryCreate(...)` for an Event, and
  build the `DueDate` with `IsAllDay` set for a Task. Existing validation messages ("An event
  requires both a start and an end time." / "A task requires a due date.") are unchanged — all-day
  still requires dates, just not meaningful times.
- `CalendarItemResponse`
  ([`CreateItem.Endpoint.cs`](../../../src/backend/buddy/Features/Calendars/CreateItem/CreateItem.Endpoint.cs))
  needs no new field — it already returns `Period`/`DueDate` directly, which now carry the flag.

## iCal feed

[`IcalFeedWriter.cs`](../../../src/backend/buddy/Features/Calendars/IcalFeedWriter.cs) currently
always writes `DtStart`/`DtEnd`/`Due` as full `CalDateTime` instants in UTC. For an all-day
occurrence this needs to become a **date-only** value (RFC 5545 `VALUE=DATE`, no time or timezone
component) using the occurrence's local calendar date — not `.UtcDateTime`, which could shift the
date across a UTC day boundary — plus `IsAllDay = true` on the `Ical.Net` `CalendarEvent` for
`VEVENT`s. `Ical.Net`'s exact API for constructing a date-only `CalDateTime` (a `DateOnly`-based
constructor vs. setting `.HasTime = false` on a full `CalDateTime`) needs confirming against the
version this project references — flagged as an implementation-time check, not a design fork,
since the semantics (date-only, no time) are unambiguous either way.

## Golden-file / event-shape impact

[`CalendarEventShapeTests.cs`](../../../src/backend/buddy.IntegrationTests/EventShapeTests/CalendarEventShapeTests.cs)
pins the exact JSON shape of every persisted event via golden files under
`EventShapeTests/GoldenFiles/Calendars/`. Adding `IsAllDay` to `Period`/`DueDate` changes the shape
of every event that embeds one, so these golden files need a matching, deliberate update (not a
silent test change): `EventItemCreated.json`, `TaskItemCreated.json`, `EventRescheduled.json`,
`TaskRescheduled.json`. This is exactly what the golden-file suite exists to catch — the test
failing on an unreviewed diff is the intended signal, not a bug in the approach.

## Frontend implications

No backend endpoint is new; the frontend needs to: thread `isAllDay` through
[`calendars.service.ts`](../../../src/frontend/buddy/src/app/core/calendars.service.ts)'s
`CreateItemRequest`/`RescheduleItemRequest`/`CalendarItemOccurrence`; add an "all day" checkbox to
both the create and edit forms in `agenda.html` for both the Event and Task branches, hiding
`app-time-select` (but not `app-date-select`) while it's checked; do the inclusive→exclusive end-date
conversion described above at the point `agenda.ts` builds the request (and the reverse conversion
when populating the edit form's end date from an existing occurrence); and render "All day" instead
of a time range in the read-only occurrence row when `occurrence.isAllDay` is set. New translation
keys follow the existing `translations/{en,da}/calendar.ts` structure. None of this requires a new
component — `date-select`/`time-select` are reused as-is, just conditionally shown.

## Decisions made

| Question | Decision |
|---|---|
| Which item kinds get all-day | Both Event and Task |
| How is "all-day" represented | Explicit `IsAllDay` flag on `Period`/`DueDate`, not inferred from a sentinel time |
| Backward compatibility for existing events | Constructor-default `IsAllDay = false`, no upcasting or backfill needed |
| Multi-day all-day event UI | Inclusive end date in the UI, converted to the existing exclusive `EndsAt` at the API boundary |
| Where duration/occurrence-expansion logic changes | Nowhere — the exclusive-end convention keeps `AddEventOccurrences`'s existing duration formula correct unchanged |

## Resolved during implementation

- **`Ical.Net` date-only API (v5.2.3, confirmed via reflection).** `CalDateTime` has a
  `CalDateTime(DateOnly date)` constructor that produces a date-only value (`HasTime = false`).
  `CalendarEvent.IsAllDay` is a **read-only, computed** property — it's derived from `DtStart`
  having no time component, not something to set directly. `Todo` has no `IsAllDay` property at
  all; setting `Due` to a date-only `CalDateTime` is sufficient for it to serialize as
  `DUE;VALUE=DATE:...`. `IcalFeedWriter.cs` uses the occurrence's local date
  (`DateOnly.FromDateTime(occurrence.StartsAt.Value.DateTime)`, not `.UtcDateTime`) when building
  these — safe because `TimeZoneResolution.ResolveInstant` constructs the `DateTimeOffset` as
  `new DateTimeOffset(local, offset)`, so `.DateTime` returns the original local wall-clock value
  unchanged, not a UTC-shifted one.

## Open questions

- **Recurring all-day items.** An all-day item that also repeats (e.g. "every year, all day") needs
  no special-casing beyond what's described here — `RecurrenceExpansion` operates on dates, and
  `IsAllDay` is a property of the item's `Period`/`DueDate`, reused unchanged across every expanded
  occurrence. Worth a dedicated integration test case, not a design change.
- **Should `RescheduleItem` allow toggling `IsAllDay` independently of changing the dates?** This
  document assumes yes — the same call that can change dates can also flip the flag, consistent
  with how `EventRescheduled`/`TaskRescheduled` already replace the whole `Period`/`DueDate` in one
  event rather than field-by-field.
