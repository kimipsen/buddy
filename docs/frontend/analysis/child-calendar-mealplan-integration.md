# Meal plan in the child calendar (`/child/calendar`) — implementation plan

Status: Proposed, not yet implemented. This is a follow-up to
[Child calendar agenda (`/child/calendar`) — implementation plan](child-calendar-agenda-plan.md),
which built the multi-day agenda this plan extends. Read it alongside
[`ChildCalendar`](../../../src/frontend/buddy/src/app/features/child/calendar/child-calendar.ts)
(`/child/calendar`) and
[`ChildMealplan`](../../../src/frontend/buddy/src/app/features/child/mealplan/child-mealplan.ts)
(`/child/mealplan`).

## Goal

As a child, see the meal plan intertwined with tasks and appointments in the
calendar view already available at `/child/calendar`, instead of only on the
separate `/child/mealplan` screen.

## Scope decision (already made)

Frontend-only — no backend or data-model change. This isn't a shortcut, it's
the documented decision for this exact case:
[docs/backend/mealplans/flow.md](../../backend/mealplans/flow.md#calendar-integration)
states plainly that a mealplan entry does **not** appear in
`Calendar`/`ListOccurrences` (the same choice already made for medicine
doses), and that a combined agenda view is meant to be solved on the
frontend, interleaving `ListOccurrences` and `ListMealPlan` results by date.
See [docs/backend/analysis/mealplans.md](../../backend/analysis/mealplans.md)
for the full rationale.

## Why meals can't just reuse the existing sort

`CalendarOccurrence` items carry real timestamps (`startsAt`/`dueAt`), which
the calendar already sorts lexicographically as ISO instants. A
`MealPlanEntry`
([`core/mealplans.service.ts:35-46`](../../../src/frontend/buddy/src/app/core/mealplans.service.ts))
only carries a `date` + a `MealSlot` enum (Breakfast/Lunch/Dinner/Snack, in
that *declaration* order, ordinals 0-3) — no real clock time. The backend has
a per-slot fallback time used only for its iCal export
([`MealSlotDefaultTimes.cs`](../../../src/backend/buddy/Features/Mealplans/MealSlotDefaultTimes.cs):
Breakfast 07:00, Lunch 12:00, Dinner 18:00, Snack 15:00), but doesn't expose
it via the API. To interleave meals chronologically with tasks/events, the
frontend needs its own copy of that same mapping (note slot *chronological*
order is Breakfast, Lunch, Snack, Dinner — not the enum's declaration order)
purely to compute a sort position; meals still display their slot name
("Breakfast"), never a fabricated clock time.

## Backend

No changes required. Confirmed already sufficient:

- `GET /mealplans/children/{childId}/plan?from&to`
  (`MealplansService.listMealPlan(scope, from, to)`,
  [`core/mealplans.service.ts:110`](../../../src/frontend/buddy/src/app/core/mealplans.service.ts)) —
  same endpoint and date-range shape the existing `/child/mealplan` screen
  already uses, and the same `from`/`to` week window `child-calendar.ts`
  already computes for `listOccurrencesInRange`.

## Frontend plan

### 1. `child-calendar.ts`

- Inject `MealplansService`.
- Resolve the signed-in child's id once via `this.users.ensureCurrentUser()`
  (memoized in `UsersService`, same pattern as
  [`child-mealplan.ts:186-187`](../../../src/frontend/buddy/src/app/features/child/mealplan/child-mealplan.ts))
  and use it to build a `{ kind: 'family', childId }` `MealplanScope`.
- Add `mealEntries = signal<MealPlanEntry[]>([])`, populated in `loadWeek()`
  by adding `this.mealplans.listMealPlan(scope, from, to)` to the existing
  `Promise.all([...])` alongside `listMyCalendars()`/`listOccurrencesInRange()`.
- Add a local mapping, commented as mirroring `MealSlotDefaultTimes.cs` so it
  doesn't silently drift from the backend fallback:
  ```ts
  const MEAL_SLOT_SORT_TIME: Record<MealSlot, string> = { 0: '07:00', 1: '12:00', 2: '18:00', 3: '15:00' };
  ```
- Add a small local row type so meals sit alongside the existing
  `AgendaEntry` union (`CalendarOccurrence | TaskRun`,
  [`core/task-run.ts`](../../../src/frontend/buddy/src/app/core/task-run.ts))
  without touching that shared file (it's also used by the guardian agenda):
  ```ts
  interface MealRow { meal: MealPlanEntry; }
  type ChildAgendaRow = AgendaEntry | MealRow;
  function isMealRow(row: ChildAgendaRow): row is MealRow { return 'meal' in row; }
  ```
- Replace `groupedOccurrencesFor(date)` with a version that merges
  `groupTaskRuns(occurrencesFor(date))` with that day's `mealEntries()`, then
  sorts the combined list by an "HH:mm" sort key: for occurrence/run rows,
  derive it from `instantFor(...)` via the existing
  `toTimeInTimeZone(instant, users.timeZoneId())` helper
  (`core/date-utils.ts`); for meal rows, `MEAL_SLOT_SORT_TIME[meal.slot]`.
  All-day occurrences keep their current sort behavior unchanged.
- Update `hasAnyVisibleOccurrence` to also check `mealEntries()` for the
  visible days, so a day with only meals (no tasks/events) still renders its
  card instead of being treated as empty.
- Add `isMeal(row: ChildAgendaRow)` for the template (wraps `isMealRow`).

### 2. `child-calendar.html`

In the `@for (entry of groupedOccurrencesFor(day.date); ...)` loop, add a
third branch alongside the existing run (`isRun`) and occurrence branches:

```html
@if (isMeal(entry)) {
  @let meal = $any(entry).meal;
  <li class="flex items-center gap-3 rounded-2xl border-2 border-slate-200 dark:border-slate-800 px-4 py-3">
    <span class="inline-block size-3 shrink-0 rounded-full" [style.background-color]="meal.color"></span>
    <span class="flex flex-1 items-center gap-2 text-lg font-semibold">
      <span aria-hidden="true">{{ meal.icon }}</span>
      {{ meal.mealName }}
    </span>
    <span class="whitespace-nowrap text-sm font-semibold text-slate-500 dark:text-slate-400">
      {{ mealSlotLabels[meal.slot] | translate }}
    </span>
  </li>
} @else if (isRun(entry)) {
  ...
```

Meal rows are display-only — no checkbox/button, no click handler — matching
the already-read-only nature of this screen and keeping rating/editing on
the dedicated `/child/mealplan` screen.

### 3. i18n

No new keys needed. Reuse
`dashboard.mealplan.slots.{breakfast,lunch,dinner,snack}`
([`en/dashboard.ts`](../../../src/frontend/buddy/src/app/core/i18n/translations/en/dashboard.ts)),
already used by `child-mealplan.ts` and `child/home/home.ts` for the same
slot labels.

### 4. Testing

In `child-calendar.spec.ts`, add a `MealplansService` stub (mirroring the
existing `CalendarsService`/`UsersService` stubs already in that spec) and
cases for:

- A meal and a task on the same day render in the expected chronological
  order (e.g. a 07:00 breakfast before a 09:00 task, and a 15:00 snack after
  a 12:00 lunch and before an 18:00-based dinner, verifying the
  chronological-not-ordinal slot mapping).
- A day with only a meal entry (no tasks/events) still renders its day card,
  not folded into the empty state.
- Meal rows show the meal's icon/name/slot label and are not clickable (no
  completion affordance).

## Explicitly out of scope for this phase

- Rating a meal from the calendar view — stays on `/child/mealplan`.
- Any change to how tasks/events are stored or served; `CalendarItemKind`
  stays a closed Event/Task enum on the backend, per the existing
  architectural decision.
- Bringing this same interleaving to the guardian agenda — this plan is
  scoped to the child view only, matching the user story it was requested
  for. If wanted later, treat it as its own follow-up.

## Verification

- `ng test` for the updated `child-calendar.spec.ts`.
- Manual: sign in as a guardian, assign meals for the current week via the
  existing meal-plan screen, then sign in as that child and open
  `/child/calendar` — confirm meals appear alongside tasks/events in time
  order, a day with only a meal still shows, and meal rows are
  non-interactive.
