# Progress badge on the child home dashboard

Status: Implemented (badge + service + guardian goal-post management; no
reward redemption UI yet)

## Context

The backend now tracks a per-child, non-resetting star count as tasks are
completed (`Features/Progress/`, see
[gamified-progress.md](../../backend/analysis/gamified-progress.md)), and a
guardian can configure the ordered list of goal posts (threshold, icon,
optional label) a child progresses through, extrapolated indefinitely past
whatever the guardian configured (see
[configurable-goal-posts.md](../../backend/analysis/configurable-goal-posts.md)).
This document covers the two frontend surfaces that exist for it: a small
badge on `/child`, the single-day dashboard described in
[A single-day dashboard for the child home screen](child-day-dashboard.md),
and a guardian-facing "Manage progress goals" screen at
`/guardian/progress`. It deliberately does not cover a reward catalog or
redemption flow — those depend on product decisions the backend doc leaves
open.

## A naming/visual collision worth calling out

The child dashboard already uses a `★` glyph prominently — tapping 1–5 stars
rates a meal (`entry.rating`, `child.mealplan.starLabel`). Reusing the same
glyph for "you earned a reward" would sit right next to "rate this meal" and
read as the same interaction, which it isn't: one is feedback the child
gives, the other is a reward the child receives. `ProgressBadge`
([progress-badge.ts](../../../src/frontend/buddy/src/app/shared/progress-badge/progress-badge.ts))
deliberately uses an icon motif (a growing plant by default — 🌱→🌿→🪴→🌳→🏆 —
or whatever icons the guardian configured) and a `✨` sparkle next to the
numeric count instead. This also reinforces the backend's own design intent
— the count only grows, it doesn't wilt on a missed day — in a way a star
rating (which the child already associates with "how good was this")
wouldn't.

The badge no longer computes its own icon from `unlockedMilestones().length`:
`ProgressSummary` now resolves `currentIcon` (the icon of the last goal post
reached, or `null` before the first one), `nextGoalThreshold`, and
`nextGoalIcon` server-side (`GoalPostResolver`, shared with the milestone
detection on the write path), so the badge is a thin renderer of whatever
the backend resolves and never re-derives the extrapolation logic. It shows
a "next goal" hint (icon + threshold) whenever one is still ahead.

The backend's internal event/type names (`StarAwarded`, `ProgressSummary`)
keep the word "star" — that's an implementation detail invisible to the
child, not something the UI needs to match.

## Where it lives, and why not gated behind `hasAnything()`

`ChildHome` (`features/child/home/home.ts`) already has one "is there
anything to show today" gate (`hasAnything()`) that decides between the
dashboard sections and a single empty-state card. The progress badge is
placed **above** that gate, always visible once loaded, because a light day
with a bare "nothing to show" card is exactly the day a child most benefits
from seeing "you're still at 12 stars" rather than the badge disappearing
along with everything else.

## Data flow

`ProgressService.getMyProgress()` calls the new `GET /progress/me` endpoint
and is loaded once in `ngOnInit` alongside the guardian/sibling lists — a
supplementary widget, not core dashboard data, so a failed load is swallowed
the same non-blocking way `loadSiblings()` already treats its own failure
(empty list, no error banner).

**Deliberate simplification: no optimistic local math.** `toggleTask()`
already awaits `CalendarsService.setTaskCompletion()`, and the backend
awards or revokes the star as part of that same request
(`SetTaskCompletionHandler` calling `RecordStarChange` — see the backend
doc). So after that call resolves, the star has already changed
server-side; `toggleTask()` simply re-fetches progress rather than
incrementing/decrementing a local counter and duplicating the backend's
milestone-threshold logic on the client. This costs one extra small request
per toggle, which is an acceptable trade for never having the client's
guess drift from the server's truth (e.g. if a milestone was crossed, or if
the same occurrence had already been awarded by a previous, retried call).

## Immediate feedback without a parent-managed flag

`ProgressBadge` detects its own increase via an `effect()` comparing the
current `totalStars` input against the previous one, and triggers a short
`animate-bounce` pulse — it doesn't need `ChildHome` to pass down a separate
"did this just change" signal. This keeps the "did something change"
tracking next to the thing that renders it, rather than threading transient
UI state through the parent component the way `savingTaskId`/`savingDoseKey`
already have to for in-flight request state (a different kind of transient
state that genuinely does need to live in the parent, since it gates which
button is disabled).

## Guardian visibility

Guardians see each linked child's star count as a small `✨ N stars` pill on
the existing children list
([children-overview.html](../../../src/frontend/buddy/src/app/features/guardian/children-overview/children-overview.html)),
next to the "Linked" badge already there — not a new widget, since the
guardian dashboard already has exactly one place that lists "my children,"
and progress is a fact about a child, not a "today" occurrence like the
other `-today` widgets on that dashboard. Backed by a new
`GET /progress/children/{childId}` endpoint
([GetChildProgress.Handler.cs](../../../src/backend/buddy/Features/Progress/GetChildProgress/GetChildProgress.Handler.cs)),
authorized the same way `MedicineAuthorization` already gates a guardian
viewing a child's medicine schedules — self, or an active `GuardianLink`,
else `NotFound`. A child with zero stars shows no pill at all (the same
"omit the empty state" convention `hasAnything()`/`mealsToShow()` already
use elsewhere on this dashboard), rather than a discouraging `✨ 0 stars`.

Each child's progress is fetched independently after the children list
loads, so one child's fetch failing doesn't blank out the whole widget or
block the (more important) name/linked-status list from rendering — the
same best-effort shape `tasks-today` already uses for its own secondary
lookup (assignee names).

## Guardian goal-post management

A guardian configures a child's goal posts on a dedicated
`/guardian/progress` screen
([progress.ts](../../../src/frontend/buddy/src/app/features/guardian/progress/progress.ts)),
reachable from the profile menu — not inline on `children-overview`, since
editing an ordered list of thresholds/icons/labels needs more room than a
row in that list. `ManageProgressGoals`
([manage-progress-goals.ts](../../../src/frontend/buddy/src/app/features/guardian/progress/manage-progress-goals/manage-progress-goals.ts))
picks the guardian's first child by default, loads that child's current
goal posts via `ProgressService.getChildProgress()`, and lets the guardian
add/remove/edit rows before saving the full list with
`ProgressService.configureGoalPosts()` — a full replace, mirroring the
backend's `GoalPostsConfigured` event semantics rather than a diff-based
update. `canSave()` requires at least one row with a positive threshold and
a non-empty icon (the same shape `ConfigureGoalPostsValidator` enforces
server-side); on success the rows are reset from the response's
`goalPosts` so the form reflects exactly what the server persisted,
including any server-side normalization.

## Deliberate boundaries

- No reward catalog or redemption UI — Phase 3 in the backend doc, blocked
  on the same open product questions (real-world vs. cosmetic rewards).
- No per-child on/off toggle for gamification, and no way for a guardian to
  manually adjust a child's stars — goal posts control the icons/thresholds
  a child progresses through, not the star count itself.
- No sibling comparison of any kind — the badge only ever renders the
  signed-in child's own `GET /progress/me` response.
- No dose-related stars shown, since the backend doesn't award any for
  doses yet (open question in the backend doc).
- No preview of extrapolated (round ≥ 1) goal posts in the management
  screen — a guardian only edits the configured list; the rounds the
  backend generates past it aren't shown or editable there.
- `home.spec.ts` and `manage-progress-goals.spec.ts` stub `ProgressService`
  the same way every other dashboard dependency is stubbed.
