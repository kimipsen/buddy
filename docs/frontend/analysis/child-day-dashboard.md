# A single-day dashboard for the child home screen

The child-facing home screen
([features/child/home](../../../src/frontend/buddy/src/app/features/child/home)) is currently a
placeholder: it shows the child's guardians and an empty "🗓️ no plan yet" card, with no real data
wired in. This document analyzes what a simple, single-day dashboard should look like there —
today's meal plan, today's medicine, and today's tasks, laid out for a child with ADHD who needs
"what's happening today" to be immediately scannable, not a calendar to navigate. Status: analysis
only — nothing here is implemented yet.

## What already exists to build on

The guardian dashboard already solves the "one child, today's data" problem three times over, in
[features/guardian](../../../src/frontend/buddy/src/app/features/guardian):

- `mealplan-today` — today's meal-plan entries, grouped by slot
- `doses-today` — today's medicine doses, with a tap-to-mark-taken/skipped control
- `tasks-today` — today's tasks, split into overdue and due-today

Each follows the same shape: a `loading`/`error`/data signal trio, populated in `ngOnInit`, backed
by a service call scoped to one child and one date
(`todayIsoDate()` from [core/date-utils.ts](../../../src/frontend/buddy/src/app/core/date-utils.ts)).
The guardian versions loop over every linked child (`GuardiansService.listMyChildren()`); the child
dashboard doesn't need that loop — there's exactly one relevant child, the signed-in user. Their own
id is already resolved on every login via `UsersService.ensureCurrentUser()`
([core/users.service.ts](../../../src/frontend/buddy/src/app/core/users.service.ts)), so no new
"who am I" plumbing is needed.

This means most of the child dashboard is a **recomposition problem, not a new-data problem**: same
three service calls, same today-only scope, restyled for a kid instead of a parent.

## Layout options considered

| Option | What it looks like | Read on sight? | Build cost |
|---|---|---|---|
| One merged timeline | Every meal, dose, and task interleaved in time order | Harder — three different item shapes (a slot, an exact time, a due time) sharing one line each | Higher — needs a shared row renderer for dissimilar data |
| Three "if any" sections (recommended) | A meals block, a medicine block, a tasks block, each only rendered when it has content | Easier — one icon, one purpose, one action per block | Lower — each block is a light restyle of an existing widget |
| Guardian-style multi-child grid | Same as the guardian dashboard, one row per child | Not applicable — there's only ever one child here | N/A, ruled out immediately |

**Recommendation: three sections, not a merged timeline.** Interleaving meals, doses, and tasks by
time forces a child to parse three different row shapes in one list to find the thing they care
about right now. Three big, icon-led, single-purpose blocks — 🍽️ meals, 💊 medicine, ✅ tasks — are
easier to scan and match how the guardian widgets already present the same data. A section is
skipped entirely when it has no data today, so a light day doesn't show three empty-state cards —
matching "keep it simple": nothing to look at except what's actually happening.

## Section-by-section design

**Meals** — `MealplansService.listMealPlan({ kind: 'family', childId: myId }, today, today)`
([core/mealplans.service.ts](../../../src/frontend/buddy/src/app/core/mealplans.service.ts)),
grouped by the same `SLOT_LABELS`/`SLOTS` ordering `mealplan-today.ts` already uses (breakfast →
lunch → dinner → snack). View-only — a child can already view their own family's plan
(`MealplanAuthorization.CheckView` treats the child themself as always allowed), and rating a meal
is a separate existing flow, not part of this dashboard.

**Medicine** — `MedicinesService.listDoses(myId, today, today)`
([core/medicines.service.ts](../../../src/frontend/buddy/src/app/core/medicines.service.ts)),
sorted by time. Each row is a large tap target cycling Pending → Taken (Skipped as a secondary,
less prominent action), calling the existing `setDoseStatus` — the backend already allows a child
to mark their own dose (`MedicineAuthorization.CheckMark` treats `callerId == childId` as allowed
today, no backend change needed). Optimistic update on tap, same map-by-key pattern
`doses-today.ts` uses, so the row flips state immediately instead of waiting on the round trip.

**Tasks** — `CalendarsService.listTodayOccurrences()`
([core/calendars.service.ts](../../../src/frontend/buddy/src/app/core/calendars.service.ts)),
filtered to `kind === Task`, sorted by due time (undated tasks last). This is the one section that
**cannot be purely a restyle**: `CalendarItem` has no completion concept anywhere in the domain
today — no `IsCompleted`, no mark-done event, nothing (confirmed by grep across
[Features/Calendars](../../../src/backend/buddy/Features/Calendars)). A tap-to-complete checkbox —
the actual payoff of a task list for a kid — needs a small backend addition first: a per-occurrence
completion flag on `CalendarItem`, the same shape `MedicineSchedule.DoseLog` already uses for
per-occurrence dose status, exposed through a new `setTaskCompletion` method alongside the existing
`setDoseStatus`. That backend piece is out of scope for this document (frontend analysis only) —
noted here only because it gates whether the tasks section ships as tap-to-complete or read-only on
day one.

One caveat worth checking before relying on tap-to-complete: whichever authorization tier gates it
determines whether every child can actually use it, or only children whose family calendar setup
happens to grant them write access. Worth confirming against how a real family's calendar sharing
is actually configured, not assumed.

## What doesn't change

No navigation is added — no previous/next day, no week view. This is a "what's happening right
now" screen, matching "keep the daily information in focus," not a calendar browser. The existing
header, sign-out button, and guardian list stay; only the empty "no plan yet" card is replaced —
and only shown when all three sections are empty, not per-section.

## Recommendation

Ship the three-section layout, reusing the existing meal-plan and medicine services as-is (both
already fully support the child viewing and, for medicine, acting on their own data). Treat the
tasks section's completion checkbox as the one piece with a real dependency — either hold the whole
dashboard for the small `CalendarItem` completion addition, or ship tasks read-only first and add
the checkbox as a fast follow once that backend piece lands.
