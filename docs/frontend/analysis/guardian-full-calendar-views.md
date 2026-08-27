# Day / work week / week / month views for the guardian calendar

Status: Planned, implementation follows this doc in the same change.

## Goal

Let a guardian browse their merged calendars (see [Creating events and seeing
them across every accessible calendar](calendar-agenda-and-event-creation.md))
in four view modes instead of only the current fixed rolling 7-day list:

- **Day** — a single day.
- **Work week** — Monday through Friday of the week containing the anchor
  date.
- **Week** — the existing view, unchanged (see "Scope decision" below).
- **Month** — a full calendar-grid month, including the leading/trailing days
  from adjacent months needed to fill complete weeks.

This is a guardian-only, frontend-only change. It does not touch calendar
authorization, occurrence expansion, or any backend endpoint — the existing
`GET /calendars` + `GET /calendars/{id}/occurrences?from&to` contract (see
`ListOccurrences.Handler`, capped at 366 days) already supports arbitrary
ranges. The child calendar (`/child/calendar`) is out of scope; it keeps its
own fixed 7-day agenda as documented in [Child calendar agenda
plan](child-calendar-agenda-plan.md).

## Scope decision: the existing "week" view keeps its exact current behavior

Today's `CalendarAgenda` (`/guardian/calendar`) shows a rolling 7-day window
starting at an adjustable anchor date (default: today), not a Monday-aligned
calendar week. Rather than redefine "week" to mean a Monday–Sunday grid (which
would change the default screen's behavior and invalidate the range/label
assertions in `agenda.spec.ts`), the **Week** mode is kept byte-for-byte
identical to today: same `buildDays` windowing, same ±7-day navigation, same
"This week" / "Previous week" / "Next week" copy. It remains the default view
mode.

**Work week** and **Month**, by contrast, are genuinely new modes and are
Monday-aligned: work week is Monday–Friday of the week containing the anchor,
month is a full Monday-start grid. This is a deliberate inconsistency (rolling
week vs. aligned work-week/month) but the lower-risk option — it adds
capability without touching a screen that already works and is already
tested.

## View-mode switching, not separate routes

The view switcher lives inside `CalendarAgenda`
(`src/app/features/guardian/calendar/agenda/agenda.ts`) as a `viewMode`
signal (`'day' | 'workweek' | 'week' | 'month'`), not a separate route or
component. Reasoning: every view mode shares the same data (merged
occurrences across accessible calendars), the same per-calendar filter, and
critically the same create/edit/delete item forms — duplicating that into
parallel routed components would either fork the write-path logic four ways
or force one of the views to be second-class. A `GuardianCalendar` sub-route
per view was considered and rejected for this reason.

## Windowing: generalizing `buildDays`, and where each view's dates come from

`date-utils.ts` gains pure date-window helpers (no date library — the app has
none and doesn't need one here):

- `parseIsoDate` / `addDaysIso` — moved here unchanged from `agenda.ts` (were
  local to that file; `child-calendar.ts` keeps its own duplicate, since it's
  out of scope for this change and touching it isn't necessary).
- `startOfWeekIso(isoDate)` — the Monday of the week containing `isoDate`.
- `startOfMonthIso(isoDate)` / `shiftMonthIso(isoDate, months)` — month
  arithmetic, clamping day-of-month (Jan 31 + 1 month → last day of
  February).
- `buildDateRangeIso(startIsoDate, dayCount)` — `dayCount` consecutive ISO
  dates from a start date. Week's existing `buildDays` becomes a one-line
  wrapper around this plus label formatting; Day and Work week both use it
  directly.
- `buildMonthGridIso(isoDate)` — every Monday-start week that intersects the
  calendar month containing `isoDate`, including the padding days from the
  previous/next month needed for complete rows (a 4-, 5-, or 6-row grid
  depending on the month, not a fixed 42-cell grid).

`CalendarAgenda.days` (already the single source of both the rendered day
list and the fetch range — `loadWeek` reads `days()[0].date` /
`days().at(-1).date`) becomes a `computed()` that branches on `viewMode()`:

| Mode | `days()` source | Fetch range this implies |
|---|---|---|
| Day | `buildDateRangeIso(anchor, 1)` | the single day |
| Work week | `buildDateRangeIso(startOfWeekIso(anchor), 5)` | Mon–Fri |
| Week | `buildDays(anchor, locale)` (unchanged) | today’s existing rolling 7 days |
| Month | `buildMonthGridIso(anchor)` | the full grid, including padding days |

Because the occurrence-fetch range is still just "first day to last day of
`days()`", **no branching is needed in `loadWeek`/`loadOccurrences` itself** —
the existing `Promise.all([listMyCalendars(), listOccurrencesInRange(from,
to)])` call is reused as-is for every mode. Month view deliberately fetches
occurrences for the padding days too (not just the 1st–last of the month),
so a guardian sees events on a visible "last Sunday of the previous month"
cell rather than a cell that looks empty because its range is naively
excluded — that padding range is at most a few days per side and stays far
under the backend's 366-day cap.

`occurrencesByDate` (the existing computed that groups the loaded occurrences
by ISO date, respecting the per-calendar hidden-set filter) is unchanged and
reused by every view — it doesn't know or care what window `days()`
currently holds.

## Navigation and the header

`previousWeek()`/`nextWeek()` become `previousPeriod()`/`nextPeriod()`,
branching on `viewMode()`:

- Day: shift the anchor by 1 day.
- Work week / Week: shift by 7 days (Week: exactly the current
  `shiftWeek(±DAYS_AHEAD)` logic, unchanged; Work week: same 7-day shift,
  then `days()` re-snaps to the new week's Monday).
- Month: `shiftMonthIso(anchor, ±1)`.

Button labels and the header title become mode-dependent translated strings
(`calendar.agenda.previousDay/nextDay`, reusing the existing
`previousWeek`/`nextWeek` keys for both Week and Work week, and new
`previousMonth`/`nextMonth`). The header title (`calendar.agenda.title`,
currently the static "This week") becomes a `viewTitle` computed: unchanged
text for Week, "Work week" for work week, the anchor date's formatted weekday
for Day, and the formatted month/year (e.g. "August 2026") for Month. Because
Week's branch returns the exact same translated string as today, this is not
a behavior change for the default view.

A **Today** button is added (jumps `anchorDate` back to `todayIsoDate()`
without changing `viewMode`) — without it, Month view in particular is easy
to navigate away from and hard to get back from quickly.

## Rendering: two shapes, one data model

**Day / Work week / Week** all render through the existing vertical
day-grouped list markup in `agenda.html` (`@for (day of days(); ...)` →
`occurrencesFor(day.date)`), completely unchanged — only the contents of
`days()` differ per mode. Create, edit, delete, and task completion keep
working exactly as they do today in every one of these three modes, since
none of that markup or its component methods change.

**Month** is the one genuinely new rendering shape — nothing in the codebase
today draws a grid. It is extracted into a new presentational child
component, `MonthGrid`
(`src/app/features/guardian/calendar/agenda/month-grid/month-grid.ts`),
rather than inlined into the already-429-line `agenda.html`:

- Inputs: `days` (the grid cells, each flagged `isCurrentMonth`),
  `weekdayLabels` (7 localized Mon–Sun headers, computed once in
  `CalendarAgenda` since it already has `translation.language()`),
  `occurrencesByDate`, and `today` (for a "today" highlight).
- Output: `daySelected` — clicking a cell's date number, or its "+N more"
  overflow label, sets `viewMode` to `'day'` and `anchorDate` to that cell's
  date in the parent. This is the intentional interaction model: **Month is
  for overview and navigation, not inline editing.** Each cell shows up to 3
  occurrence chips (colored dot + truncated title, no checkbox, no
  edit/delete affordance) with a "+N more" overflow — cramming the existing
  edit/create/delete forms into a ~120px grid cell was rejected as both a
  layout and an interaction-model problem. A guardian who wants to act on a
  specific day's items clicks into Day view for that date, where the full
  existing toolkit (create, edit, delete, task completion) is already
  present and unchanged.
- Non-current-month padding cells render visually dimmed; the create/edit
  forms below the calendar body stay exactly where they are today (outside
  and independent of whichever view is currently rendered).

## i18n

New keys under `calendar.agenda.*` in both
[`en/calendar.ts`](../../../src/frontend/buddy/src/app/core/i18n/translations/en/calendar.ts)
and
[`da/calendar.ts`](../../../src/frontend/buddy/src/app/core/i18n/translations/da/calendar.ts):
`view.day`, `view.workweek`, `view.week`, `view.month`, `today`,
`previousDay`, `nextDay`, `previousMonth`, `nextMonth`, `dayTitle` (takes a
`{date}` param), `workweekTitle`, `monthTitle` (takes a `{month}` param), and
`monthGrid.moreLabel` (takes a `{count}` param) — following the existing
`interpolate()`/`{param}` convention already used elsewhere in
`TranslationService`.

## Testing

- `agenda.spec.ts`: every existing test keeps passing unmodified, since
  Week's behavior, button text, and header text are untouched. New tests are
  added for: switching to each mode changes the requested `from`/`to` range
  correctly; Day/Work week/Month navigation shifts the anchor and re-fetches
  by the right amount; Work week only ever renders 5 day buckets (never
  Saturday/Sunday); Month's fetched range includes the grid's padding days.
- `month-grid.spec.ts` (new): grid cell count matches a full Monday-start
  week set for a given month; padding days are flagged
  `isCurrentMonth: false`; occurrence chips are capped with a correct "+N
  more" count; clicking a cell/chip/overflow emits `daySelected` with that
  cell's date.

## Explicitly out of scope

- Any change to `/child/calendar` or its fixed 7-day list.
- Any backend change — range cap, authorization, or occurrence expansion.
- Drag-to-reschedule or any other month-grid-specific editing interaction
  beyond "click a day to open Day view".
- A batched "occurrences across N calendars in one request" endpoint. Month
  view's grid range (~35–42 days) still fans out one HTTP request per
  accessible calendar via `listOccurrencesInRange`, same as every other mode
  today; this is an existing characteristic of the service, not something
  this change introduces or needs to fix.
