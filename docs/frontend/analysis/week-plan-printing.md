# Week plan printing

Status: Implemented. `PrintTemplatesService`, `isoWeekNumber`/`nextWeekdayOnOrAfter`, the pure
`assembleWeekPlan`, `WeekPlanLoader`, `WeekPlanSheet`, the print route (outside the shell) and its
`@page` handling, quick print at `/guardian/print` and the template editor at
`/guardian/print/templates/:templateId` all shipped, with Vitest specs and a Playwright check that
A4 and A3 each print as exactly one landscape page. See "Implementation notes" for where the build
refined this design.

A guardian prints a landscape week plan on A3 or A4 paper from a saved
template, choosing only the start date. The template model, ownership, and
authorization are designed in
[week-plan-print-templates.md](../../backend/analysis/week-plan-print-templates.md);
this page covers the Angular side: routes, data loading, the print layout, and
the template editor.

## Routes

| Route | Component | Shell |
|---|---|---|
| `/guardian/print` | `GuardianPrint` — template list and quick print | Inside `GuardianShell` |
| `/guardian/print/templates/:templateId` | `PrintTemplateEditor` | Inside `GuardianShell` |
| `/guardian/print/sheet/:templateId?start=YYYY-MM-DD` | `WeekPlanPrintPage` | **Outside** `GuardianShell` |

The sheet route is a sibling of the shell route in `GUARDIAN_ROUTES`, not a
child of it, so no navigation, header, or padding ends up on paper. It still
sits under `/guardian`, so the existing `authGuard` and role routing apply
unchanged. A missing or invalid `start` falls back to the next occurrence of
the template's `defaultStartWeekday`.

## Quick print flow

`GuardianPrint` is the everyday path:

1. List templates from `PrintTemplatesService.list()`.
2. The guardian picks a template and a start date. The existing `date-select`
   is pre-set to the next `defaultStartWeekday` on or after today.
3. "Preview" opens the sheet route. The preview has a small toolbar (hidden
   in print) with **Print**, **Change date** and **Edit template**.
4. **Print** calls `window.print()`.

The last-used template is remembered per device in `localStorage`, the same
lightweight approach `theme-storage.ts` takes. It is a convenience, not
configuration, so it isn't synced.

## Data loading

A new `WeekPlanLoader` (in `features/guardian/print/`) turns a template and a
start date into a `WeekPlanModel`. It only calls services that already exist:

| Row kind | Call |
|---|---|
| `Meal` | `MealplansService.listMealPlan(scope, from, to)` with a `family` scope (`ChildId`) or `group` scope (`MealGroupId`) |
| `Pickup` | `PickupsService.listSchedule(childId, from, to)`, plus `GuardiansService.listChildGuardians(childId)` and `listMyChildren()` for names |
| `WorkLocation` | `WorkLocationsService.listWorkDays(guardianId, from, to)` (new, see [work-locations.md](../../backend/analysis/work-locations.md)) |
| `CalendarMarker`, `CalendarEvents`, `TaskChecklist` | `CalendarsService.listOccurrences(calendarId, from, to)` for each calendar id |

- `from` is the start date and `to` is `addDaysIso(from, 6)`.
- Requests are deduplicated by key (the same calendar named in three rows is
  fetched once) and run through the existing `mapWithConcurrency` helper.
- Each source resolves independently. A rejected request (typically
  `NotFound` because the printing guardian can't see it) marks just the rows
  that depend on it as unavailable. The sheet never fails as a whole.

### Assembly is a pure function

`assembleWeekPlan(template, days, sources) → WeekPlanModel` is a plain
function with no Angular dependencies. All the rules that matter live there
and are unit-tested in Vitest:

- **Columns:** `buildDateRangeIso(start, 7)`, in order, so whichever day was
  picked is column one. Day headers use the user's locale (`Intl` weekday
  names, matching the en/da dictionaries).
- **Day bucketing:** an occurrence lands in every column its local
  `[startsAt, endsAt)` overlaps. Tasks use `dueAt`. All-day items sort first,
  then by start time.
- **Work location rows:** with `workLocationId`, a day gets a mark when its
  resolved location has that id. Without it, the cell shows the location's
  icon and name in the guardian's template color. A day with no location
  (`source: None`, or an override to "off") is left blank.
- **Marker rows:** a day gets a mark if any occurrence on that day matches
  `titleFilter` (case-insensitive contains; no filter means any occurrence).
- **Filtering:** `assignedToId` keeps only occurrences whose `assignedTo`
  matches. `TaskChecklist` additionally keeps only `kind === 'Task'`. For
  routine tasks scheduled from a template, subtasks are grouped under
  `parentTitle`, as the child agenda already does.
- **Overflow:** a cell shows at most `maxItems` entries, then "+N".
- **Pickup cells:** the `DropOff` occurrence fills the upper triangle and
  `PickUp` the lower one. `Guardian` shows the guardian's name in their
  template color; `Sibling`, `SelfEscort` and `Playdate` show short labels
  (`"Viggo"`, `"Selv"`, `"Leg: Emma"`). A missing occurrence is left blank so
  it can be filled in by hand.
- **Week number:** an ISO-8601 week number for the first and last column. A
  Monday start gives "Uge 40"; any other start spans two weeks and prints
  "Uge 40–41". `date-utils.ts` has no week-number helper today, so
  `isoWeekNumber(isoDate)` is added there with its own spec, including the
  year-boundary cases (e.g. 2026-12-31 is week 53, 2027-01-04 is week 1).

## The sheet

`WeekPlanSheet` is a presentational component: input `WeekPlanModel`, no
service calls. Keeping it pure means the preview, the print output, and the
editor's live preview are the same component.

### Paper size

`@page` rules can't be scoped to a component (Angular's emulated
encapsulation rewrites selectors, and `@page` has none), so
`WeekPlanPrintPage` injects a single global `<style>` element on init and
removes it on destroy:

```css
@page { size: A4 landscape; margin: 8mm; }   /* or A3 landscape */
```

The sheet's own box is sized in millimetres from the same choice:

| Paper | Page | Printable area (8 mm margins) |
|---|---|---|
| A4 landscape | 297 × 210 mm | 281 × 194 mm |
| A3 landscape | 420 × 297 mm | 404 × 281 mm |

A3 uses the same layout with font sizes and line widths scaled by √2
(≈ 1.414), the ratio between the two formats, so a template looks the same on
both — just bigger.

### Layout

- CSS grid: one fixed-width label column (about 12% of the width) plus seven
  equal day columns.
- A header row with the week number and day names, then one grid row per
  template row, sized by `heightWeight` as `fr` units. The sheet therefore
  always fills exactly one page, however many rows the template has.
- Cell content clips (`overflow: hidden`) rather than growing; `maxItems`
  plus "+N" keeps that rare.
- `break-inside: avoid` on the sheet, and the toolbar uses Tailwind's
  `print:hidden`.
- The pickup diagonal is an absolutely positioned inline SVG line, which
  prints crisply at any size, unlike a rotated border.

### Ink and theme

The print sheet always renders light, whatever the app theme. The component
sets its own colors instead of relying on theme tokens, and the injected
print style forces a white background. Color is used only where it carries
meaning (guardian names); lines are thin mid-grey; checkboxes are empty
outlined squares. Chrome and Safari print backgrounds off by default, so
nothing important depends on a fill color.

### Screen preview

On screen the sheet keeps its millimetre size and is scaled down with a CSS
`transform: scale()` computed from the viewport width, so the preview is a
faithful miniature of the paper. The transform is removed under
`@media print`.

## Template editor

`PrintTemplateEditor` edits one template:

- **Header:** name, paper size (`segmented-control`: A4 / A3), default start
  weekday, and a "show week number" `toggle`.
- **Rows:** a reorderable list built on `repeatable-row`. Each row has a
  label, kind, height weight (`stepper`), and the fields that kind needs —
  child picker, guardian picker plus a location picker from that guardian's
  `GetWorkLocationSchedule` (for `WorkLocation`), calendar multi-select from
  `CalendarsService`, meal slot,
  assignee filter, title filter, max items. Fields a kind doesn't use are
  hidden, and cleared before saving so the backend validator never sees stray
  values.
- **Guardian colors:** one `color-swatch-picker` per guardian of the
  guardian's children.
- **Live preview:** a scaled `WeekPlanSheet` for the coming week, refreshed
  as the guardian edits.
- **Stale references:** rows pointing at a calendar or child the guardian can
  no longer see are flagged inline with a fix-it prompt.

Saving sends only what changed: `PATCH …/name`, `PATCH …/layout`,
`PUT …/rows`, `PUT …/colors`. As with the pickup planner, there is no
optimistic update; the editor replaces its state with the server's response.

### Starting from the example

A new template can start empty or from an example that mirrors a typical
family sheet: dinner, drop-off/pick-up per child, a work location row per guardian location, an
activities row and a chores checklist per child, appointments, and a blank row.
The example is built client-side from the guardian's own children and
calendars and only saved when they press save.

## Services and files

| File | Purpose |
|---|---|
| `core/print-templates.service.ts` | CRUD over `/print-templates`, following `pickups.service.ts` |
| `core/work-locations.service.ts` | Comes with the work locations feature; the loader only uses `listWorkDays` and the editor `getSchedule` |
| `core/date-utils.ts` | add `isoWeekNumber` and `nextWeekdayOnOrAfter` |
| `features/guardian/print/print.ts` | `GuardianPrint` — list and quick print |
| `features/guardian/print/editor/` | `PrintTemplateEditor` |
| `features/guardian/print/sheet/week-plan-sheet.ts` | presentational sheet |
| `features/guardian/print/sheet/week-plan-print-page.ts` | route component: loads, injects `@page`, toolbar |
| `features/guardian/print/week-plan-loader.ts` | fetches sources |
| `features/guardian/print/assemble-week-plan.ts` | pure assembly function |
| `core/i18n/translations/{en,da}/print.ts` | UI strings |

A "Print week plan" entry is added to the guardian shell navigation and the
dashboard.

## Localization

All UI strings go into a new `print` dictionary in both `en` and `da`, checked
with `node .claude/skills/i18n/check-parity.mjs`. **Row labels are user data**
("Signes aktiviteter") and are printed as typed, never translated. Day names
and the week label ("Week" / "Uge") come from the printing guardian's language.

## Testing

- **Vitest:** `assembleWeekPlan` (column rotation for every start weekday,
  multi-day and all-day items, filters, overflow, pickup kinds, unavailable
  sources), `isoWeekNumber` (year boundaries), and the loader's deduplication
  and partial-failure handling.
- **Component specs:** the editor shows and clears kind-specific fields; the
  print page injects and removes exactly one `@page` style.
- **Playwright:** for both A4 and A3, open the sheet route with a seeded
  template and `page.pdf({ preferCSSPageSize: true })`, then assert that the
  PDF has exactly one page with landscape dimensions. Add a screenshot of the
  sheet for visual review, following the e2e skill's conventions.

## Accessibility

The sheet is a real `<table>`-equivalent grid with row and column headers
(`role="rowheader"` / `columnheader`), so screen readers can read the preview.
The toolbar is keyboard-reachable, and Print is the default focused action on
the preview.

## Implementation notes

Where the build settled details this design left open:

- **Events rows list tasks too.** `CalendarEvents` shows every occurrence (events and tasks);
  only `TaskChecklist` restricts to tasks.
- **Unavailable calendar rows.** If any one of a row's calendars fails to load, the whole row
  prints as "not available" rather than a partial list that would look complete.
- **Checklists print a routine once.** Subtasks scheduled from a task template collapse to one
  tick box under their `parentTitle`. Grouping is per item id, so two different tasks that share
  a title stay two tick boxes.
- **Unfinished rows print blank.** A row with no source picked yet (no calendar, child or
  location) renders as an empty cell, not "not available"; that note is only for sources the
  printing guardian can't read.
- **Pickup cells for screen readers.** The diagonal split is visual only, so each half carries a
  visually hidden "Drop-off"/"Pick-up" label, and every ✕ mark has a hidden text label.
- **Work-location text uses the guardian's template color**, falling back to the default ink, not
  the location's own color.
- **The editor lists the guardian themself first** among work-location guardians and name
  colors, even with no children linked.
- **Client-side limits mirror the backend.** The editor blocks Save on a label over 40
  characters or more than 10 calendars per row, instead of surfacing the server's `400`.
- **Stale references can be fixed.** Calendars the guardian can't see stay listed as "unknown"
  and can be unticked; rows pointing at an archived work location are flagged as stale.
- **Save status** ("Template saved.") compares the draft with the last saved draft, so it
  disappears as soon as the guardian edits again.
- **The example is capped at 12 rows** (the backend limit), always keeping its trailing notes row.
- **Live preview** is a fixed 384 px wide miniature for both paper sizes; its data refetches
  (debounced) only when the set of referenced sources changes, not on every keystroke.
- **Focus.** Print gets focus programmatically once the sheet is ready, instead of the
  `autofocus` attribute (which the template a11y lint rule rejects). It happens on the first load
  only; changing the start date doesn't pull focus back.
- **`@page` lifetime.** The print page injects one `<style data-week-plan-print>` and removes it
  on destroy; if the page is destroyed before loading finishes, the style is never added, so a
  stale paper size can't leak into later prints in the tab.
- **Races.** Quick print and the editor ignore responses for anything but the latest template
  selection or preview request, and the debounced preview timer is cleared on destroy.
- **A3 is A4 scaled by √2**, so the type and row proportions are identical on both papers.
- **Dashboard entry** is a "Print week plan" link in the dashboard header, plus the profile-menu
  entry.
- **The e2e journey** uses the example's notes row only, keeping it short (with the
  work-location setup it took ~19 s and timed out with parallel workers on a cold dev server); what each row kind
  prints is covered by the `assembleWeekPlan` and `WeekPlanSheet` specs.

## Open questions

- **Browser differences.** Safari historically ignores some `@page size`
  values and may need the paper chosen in the print dialog. If that proves
  common, the server-side PDF option from the backend document becomes the
  fallback.
- **Printing for several weeks at once** (e.g. a month of plans). Out of scope
  for v1; the sheet component already supports it if the page loops over
  start dates with page breaks.
- **iPad.** Printing from the installed web app
  ([ipad-installation.md](ipad-installation.md)) goes through the share sheet;
  worth a manual check before calling the feature done.
