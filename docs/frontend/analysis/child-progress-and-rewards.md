# Progress badge on the child home dashboard

Status: Sketch implemented (widget + service; no reward redemption UI yet)

## Context

The backend now tracks a per-child, non-resetting star count as tasks are
completed (`Features/Progress/`, see
[gamified-progress.md](../../backend/analysis/gamified-progress.md)). This
document covers the one frontend surface that exists for it so far: a small
badge on `/child`, the single-day dashboard described in
[A single-day dashboard for the child home screen](child-day-dashboard.md).
It deliberately does not cover a reward catalog, redemption flow, or
guardian-facing controls — those depend on product decisions the backend
doc leaves open.

## A naming/visual collision worth calling out

The child dashboard already uses a `★` glyph prominently — tapping 1–5 stars
rates a meal (`entry.rating`, `child.mealplan.starLabel`). Reusing the same
glyph for "you earned a reward" would sit right next to "rate this meal" and
read as the same interaction, which it isn't: one is feedback the child
gives, the other is a reward the child receives. `ProgressBadge`
([progress-badge.ts](../../../src/frontend/buddy/src/app/shared/progress-badge/progress-badge.ts))
deliberately uses a growing-plant motif (🌱→🌿→🪴→🌳, keyed off how many
milestones are unlocked) and a `✨` sparkle next to the numeric count instead.
This also reinforces the backend's own design intent — the count only grows,
it doesn't wilt on a missed day — in a way a star rating (which the child
already associates with "how good was this") wouldn't.

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

## Deliberate boundaries

- No reward catalog or redemption UI — Phase 3 in the backend doc, blocked
  on the same open product questions (real-world vs. cosmetic rewards).
- No guardian-facing progress view or per-child on/off toggle.
- No sibling comparison of any kind — the badge only ever renders the
  signed-in child's own `GET /progress/me` response.
- No dose-related stars shown, since the backend doesn't award any for
  doses yet (open question in the backend doc).
- `home.spec.ts` now stubs `ProgressService` the same way every other
  dashboard dependency is stubbed, but no dedicated test was added for the
  badge's own bounce/growth-stage behavior — this sketch prioritized an
  end-to-end wire-up (backend event → endpoint → widget) over full test
  coverage of the new pieces.
