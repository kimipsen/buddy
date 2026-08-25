# Historical meal plans and children's ratings

The guardian meal-plan screen only ever shows a rolling forward week, and there is no way for a
guardian or child to look at a past week's plan. Children also have no way to actually rate a meal
— the backend rating feature is fully built but has zero UI consumers today. This document analyzes
what's needed to let a family look back at previous meal plans and see (or leave) the children's
reviews. Status: analysis only — nothing here is implemented yet.

## What already exists to build on

This turns out to be mostly a UI gap, not a backend one:

- **Full history is already retained forever.** Mealplans are Marten event-sourced aggregates
  ([Types/MealPlan.cs](../../../src/backend/buddy/Features/Mealplans/Types/MealPlan.cs),
  [Types/Meal.cs](../../../src/backend/buddy/Features/Mealplans/Types/Meal.cs)) — assignments and
  ratings are appended events (`MealAssignedToSlot`, `MealSlotCleared`, `MealRated`), never
  overwritten. No migration or data-retention work is needed to "add" history; it's already there.
- **The list endpoint already accepts arbitrary past date ranges.**
  [ListMealPlan.Endpoint.cs](../../../src/backend/buddy/Features/Mealplans/ListMealPlan/ListMealPlan.Endpoint.cs)
  takes `from`/`to` `DateOnly` query params, capped at 31 days
  ([ListMealPlan.Handler.cs:10](../../../src/backend/buddy/Features/Mealplans/ListMealPlan/ListMealPlan.Handler.cs#L10)).
  Nothing rejects a range in the past — a request for last month works against today's backend with
  zero changes.
- **Child ratings are already a complete backend feature that nothing calls.** `PUT
  /mealplans/children/{childId}/meals/{mealId}/rating`
  ([RateMeal.Endpoint.cs](../../../src/backend/buddy/Features/Mealplans/RateMeal/RateMeal.Endpoint.cs))
  takes `{Stars, Comment}`, is guarded by `MealplanAuthorization.CheckRate`
  ([MealplanAuthorization.cs:74](../../../src/backend/buddy/Features/Mealplans/MealplanAuthorization.cs#L74))
  so only the child themself can rate, and records a `MealRated` event. But
  [mealplans.service.ts](../../../src/frontend/buddy/src/app/core/mealplans.service.ts) has no
  method that calls it, and there is no child-facing meal-plan screen at all —
  [features/child](../../../src/frontend/buddy/src/app/features/child) only has `home/`
  (guardian list + sign-out), so the rating flow is currently unreachable from the app.
- **The rating data shape already distinguishes "my rating" from "everyone's ratings."**
  `MealPlanEntry.rating` (used by the plan-range view, e.g.
  [mealplan-today.ts](../../../src/frontend/buddy/src/app/features/guardian/mealplan-today/mealplan-today.ts))
  carries only the viewing child's own `MealRating`. `Meal.ratings` (used by the meal-library
  editor) already carries every sibling's `MealRatingSummary[]`
  ([mealplans.service.ts:34-51](../../../src/frontend/buddy/src/app/core/mealplans.service.ts#L34-L51)).
  Both shapes exist server-side; the plan-range view just hasn't been given the wider one.

## Two independent problems

"Show previous meal plans, with children's reviews" is really two separate, independently shippable
pieces:

1. **Browsing past weeks** — currently impossible in the UI at any role, even though the backend
   already supports it.
2. **Reviews being visible/usable at all** — currently impossible at any role, past or present,
   because no screen calls the rating endpoint or renders the wider rating shape.

Solving only (1) would let a guardian look at last week's plan but still see at most their own
child's single current rating per meal, not a comparison across kids. Solving only (2) would let a
child rate meals but only ever the current week. Both are needed for "previous meal plans with
children's reviews."

## Problem 1: browsing past weeks

[assign-mealplan.ts](../../../src/frontend/buddy/src/app/features/guardian/mealplan/assign-mealplan/assign-mealplan.ts)
hardcodes a forward-only window: `DAYS_AHEAD = 7`
([assign-mealplan.ts:18](../../../src/frontend/buddy/src/app/features/guardian/mealplan/assign-mealplan/assign-mealplan.ts#L18)),
and builds `[today .. today+6]` unconditionally. The fix is additive: a prev/next week control that
shifts the anchor date backward or forward and re-calls the existing `listMealPlan(scope, from, to)`
— no new endpoint, no new service method for this part.

One product decision worth making explicit: **should a past week's assignments be editable?**
Nothing in the backend currently blocks assigning a meal to a past date. I'd recommend making past
weeks read-only in the UI (hide the assign/clear controls once the week is behind today) — "what did
we actually plan" is a record, and silently letting a guardian rewrite last Tuesday's dinner after
the fact undermines that record without an explicit backend decision to support it (e.g., an
"amend" concept with its own event). That's a UI-only guard, not a backend change, unless the
product intent is actually to allow retroactive edits.

Worth flagging, not blocking: because `MealPlan.Rehydrate` folds assignments down to final state per
`(date, slot)` (last write wins), a past-week view shows what a slot's assignment *ended up as*, not
a timeline of every change made to it. That's almost certainly the right answer for "what did we
eat" — flagging only because a full edit-audit view would need to replay events up to a point in
time instead of reading current aggregate state.

## Problem 2: making children's reviews visible and usable

This splits by audience:

**The child's own rating screen** (net new). Needs a new feature area under `features/child/`
(there's nothing to extend — `home/` is guardian-list-only today), showing the child's plan entries
for a date range with a star/comment control that calls the not-yet-existing `rateMeal` client
method. That method is a thin addition to
[mealplans.service.ts](../../../src/frontend/buddy/src/app/core/mealplans.service.ts), mirroring
`assignMealToSlot`'s shape but hitting the `rating` route. Authorization is already correct
server-side (`CheckRate` restricts this to the child themself), so this is pure UI plumbing on top
of a finished backend contract.

**The guardian's view of ratings when looking back.** Today `MealPlanEntry.rating` only carries the
viewing child's own rating, which is the wrong shape for a guardian comparing what each of several
children thought of the same planned meal. This needs a small, real backend change: widen
`MealPlanEntry`
([Types/MealPlanEntry.cs](../../../src/backend/buddy/Features/Mealplans/Types/MealPlanEntry.cs)) to
carry the full per-child ratings map (the same data `MealResponse.Ratings` already exposes for the
meal-library view), so a guardian looking at a past week sees every child's star/comment on a given
planned meal, not just one. This is the one piece in this document that isn't purely additive UI —
it changes an existing response contract, so it should ship with its own review rather than being
bundled invisibly into the frontend work.

## What doesn't change

No event-replay or audit-trail UI (see the point-in-time caveat above — final state is the target,
not a change history). No new domain concepts — `Child` is still just a `UserId` reached through
`GuardianLink`, and rating is still one `MealRating` per child per meal, not a log of every time a
child was asked.

## Recommendation

Ship in this order:

1. **Past-week navigation for the existing (guardian) mealplan screen** — cheapest, backend-free,
   and immediately makes "previous meal plans" real for the audience that already has a screen.
2. **Child rating screen** — the larger but highest-value piece, since it turns on a backend feature
   that has existed with zero consumers. Add the `rateMeal` service method and a new
   `features/child/mealplan` area reusing the guardian mealplan screen's data-fetch pattern.
3. **Widen `MealPlanEntry` to carry all children's ratings** — do this once (1) and (2) are in place
   and it's clear what shape the guardian's historical view actually wants to render (single rating
   chip vs. a per-child row), since designing the response shape before the consuming UI exists
   risks guessing wrong.
