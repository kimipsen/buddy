# Weekday Recurrence Rules

Status: Implemented. `Weekdays` flags on `Recurrence.Repeating` (daily rules limited to some
weekdays, weekly rules on several weekdays), accepted as `weekdays` by `POST .../items`,
`POST .../items/from-template` and `PATCH .../recurrence`, expanded by `RecurrenceExpansion`, and
weekday toggles under Daily and Weekly in the agenda create form.

## Context

A repeating calendar item is a
[`Recurrence.Repeating`](../../../src/backend/buddy/Features/Calendars/Types/Recurrence.cs)
(`Frequency`, `IntervalCount`, `End`). Every consumer of it (the occurrence list, the child home
screen, today's tasks, print and the iCal feed) goes through
[`RecurrenceExpansion.ExpandDates`](../../../src/backend/buddy/Features/Calendars/RecurrenceExpansion.cs)
via [`CalendarOccurrenceExpansion`](../../../src/backend/buddy/Features/Calendars/CalendarOccurrenceExpansion.cs).
The iCal feed writes one `VEVENT`/`VTODO` per expanded occurrence and never an `RRULE`.

The ask:

- a daily item that skips some weekdays, or runs only on chosen ones (a school-morning routine
  Monday to Friday, a task every day except Sunday);
- a weekly item on several weekdays, every N weeks (swimming Monday and Thursday every other week,
  or the whole week every other week for alternating custody).

Before this, a guardian had to create one weekly item per weekday.

## Decision: a `[Flags] Weekdays` value on `Recurrence.Repeating`

`Repeating` gains `Weekdays Weekdays = Weekdays.None`, where
[`Weekdays`](../../../src/backend/buddy/Features/Calendars/Types/Weekdays.cs) is a `[Flags]` enum
(`Monday = 1` ... `Sunday = 64`, `All = 127`).

- `None` means **no filter**: a daily rule runs every day and a weekly one on the seed's weekday,
  exactly as before. Any other value names the days explicitly.
- `None` rather than `All` is the default because a weekly rule on all seven days is a real choice
  ("every other week, all week") that differs from "the seed's weekday". A daily rule on all seven
  days is the same as no filter, so the wire normalises it to `None`; a weekly one keeps `All`.
- A flags value has structural equality, so `UpdateItemRecurrenceHandler`'s "re-sending the stored
  recurrence is a no-op" check (`item.Recurrence.Equals(command.Recurrence)`) keeps working. An
  `IReadOnlySet<DayOfWeek>` inside the record would compare by reference and break it.

Rejected: a new `RecurrenceFrequency.Weekdays` value. It would cover Monday to Friday only.

## Decision: how the days expand

- **Daily**: steps from the seed as before and drops each date whose weekday isn't selected.
- **Weekly**: every `IntervalCount` Monday-start weeks counted from the seed's week, each selected
  weekday in that week (RFC 5545's `FREQ=WEEKLY;BYDAY=...` with `WKST=MO`).

In both, dates before the seed are skipped, and the seed itself isn't an occurrence when its
weekday isn't selected. Nothing assumes the seed is always one: task completion is keyed by
`(OccurrenceDate, Target)` and `SetTaskCompletion` doesn't check the date against the rule.

Rejected: RFC 5545's "DTSTART is always the first occurrence". That's a known source of surprise,
and Buddy never writes an `RRULE`, so it doesn't need that behaviour.

## Decision: rules

`RecurrenceRules` (shared by `CreateItemValidator`, `ScheduleTaskFromTemplateValidator` and
`UpdateItemRecurrenceHandler`) adds these rules, keyed `Recurrence.Weekdays` unless noted:

| Rule | Why |
| --- | --- |
| At least one valid weekday (an empty list or a day outside 0-6 is a 400, not "no filter") | An empty filter never occurs. |
| Only for `Daily` and `Weekly` | Monthly/yearly "on weekdays" ("first Monday of the month") is a different rule shape. |
| Daily: `IntervalCount` must be 1 (`Recurrence.IntervalCount`) | "Every 2 days, weekdays only" is legal in RFC 5545 but confusing, and an interval of 7 with the seed's weekday unselected would never occur. Weekly keeps its interval. |
| An `Until` must leave at least one occurrence | Seed Saturday, until Sunday, Monday to Friday only gives an item with no dates. Checked only when `Until` is within one cycle of the seed (7 days, or `IntervalCount` weeks), which is the only case where it can happen. |

## Persistence

`RecurrenceJsonConverter` writes `"Weekdays": ["Monday", ...]` only when the value isn't `None`,
and reads a missing property as `None`. Existing events, snapshots and golden files are unchanged;
no upcaster or rebuild is needed. New golden file: `RecurrenceUpdated_ToWeekdays.json`.

## HTTP

`RecurrenceRuleRequest` gains `IReadOnlyList<DayOfWeek>? Weekdays = null` (numbers on the wire,
`0 = Sunday` ... `6 = Saturday`, like JavaScript's `Date.getDay()`). `null` means no filter. The
response (`CalendarItemResponse.Recurrence`) returns `null` for no filter and the days Monday first
otherwise.

## Frontend

The agenda create form shows seven weekday toggles under Repeat for Daily and Weekly.

- **Daily**: all on by default. When any day is off, the "Every" stepper is hidden and the request
  sends `intervalCount: 1` and the selected days.
- **Weekly**: the start date's weekday is on by default and follows the start date until the
  guardian touches a toggle; untouched sends `weekdays: null`, touched sends the selected days.
  The stepper stays (every N weeks).
- Changing the Repeat choice resets the toggles. With no day on, submit is disabled.

## Decisions made

| Question | Decision |
| --- | --- |
| Model | `[Flags] Weekdays` on `Repeating`, default `None` (no filter) |
| Seed on an unselected day | Not an occurrence |
| Frequencies | Daily and Weekly |
| Daily interval with a filter | Must be 1 |
| Weekly week start | Monday |
| Stored JSON | Omitted when `None`; string array of day names otherwise |
