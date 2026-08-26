# Historical meal plans and children's ratings

Status: Implemented

Buddy now supports week navigation for guardian and child meal-plan views,
child-authored ratings and comments, and guardian visibility into every child's
rating for past meals. The backend's existing event-sourced history and rating
operation were reused; the plan-entry response was expanded to include rating
summaries needed by the guardian UI.

## Backend contract

The list endpoint accepts arbitrary date ranges up to 31 days, including dates
in the past. `MealPlan.Rehydrate` folds assignment events into the current meal
for each `(date, slot)`, while meal ratings remain keyed by child.

[`MealPlanEntry`](../../../src/backend/buddy/Features/Mealplans/Types/MealPlanEntry.cs)
now returns both:

- `rating`, the viewing child's own rating where applicable;
- `allRatings`, summaries for every child who rated the meal.

Only the child identified by `childId` can call
`PUT /mealplans/children/{childId}/meals/{mealId}/rating`. Guardians can read
rating summaries but cannot author a child's opinion.

## Guardian week history

[`AssignMealplan`](../../../src/frontend/buddy/src/app/features/guardian/mealplan/assign-mealplan/assign-mealplan.ts)
has previous/next week controls and reloads the existing seven-day range for
the selected family or group scope.

Past dates are intentionally read-only. Assign, clear, and drag/drop controls
are disabled for them, preserving the historical plan shown to the family.
For a past family-plan entry, the guardian can inspect `allRatings`; child IDs
are resolved to loaded child names for display. A group-shared plan also obeys
its returned access tier, so viewers cannot modify current or future entries.

The screen displays the aggregate's final assignment for a historical slot,
not a timeline of every reassignment. A forensic edit-history view would need a
separate event-history presentation and is not part of this workflow.

## Child mealplan and ratings

`/child/mealplan` is implemented by
[`ChildMealplan`](../../../src/frontend/buddy/src/app/features/child/mealplan/child-mealplan.ts).
It opens on the seven days immediately before today because recent meals are the
most likely items to rate. Previous and next controls move the anchor in
seven-day increments and call `MealplansService.listMealPlan()` for the visible
range.

The child can rate an entry dated today or earlier:

- tapping a star submits immediately while retaining an existing comment;
- opening the comment editor allows explicit save/cancel behavior;
- if no star exists when a comment is first saved, the UI uses five stars;
- a successful response updates every visible slot that references the same
  meal.

Future meals remain visible when navigated to but cannot be rated before they
are served. Rating errors leave the server-backed entry unchanged and show a
translated error.

The child home also allows a quick rating for today's planned meals. The full
mealplan route adds history navigation and the wider date context.

## Service integration

[`MealplansService`](../../../src/frontend/buddy/src/app/core/mealplans.service.ts)
contains the implemented `rateMeal()` client method and models both `rating`
and `allRatings` on `MealPlanEntry`. Family plans use child-keyed routes; plans
shared with groups use group-keyed routes through the same `MealplanScope`
abstraction.

Dates are parsed as local calendar components rather than UTC timestamps. This
avoids shifting an ISO `YYYY-MM-DD` into the previous day in negative UTC
offsets. Labels follow the current application language, and all static strings
exist in the English and Danish mealplan dictionaries.

## Deliberate boundaries

- Ratings remain one current rating per child per meal, not a history of every
  rating change.
- Historical plans show final slot state, not event-by-event edits.
- Past guardian slots are read-only in the frontend. The backend does not yet
  enforce a general prohibition on retroactive assignment, so another API
  client could still request one.
- There is no aggregate rating score; the guardian sees individual child
  summaries so differing opinions are not flattened.

These boundaries preserve the distinction between “what was planned and how
each child rated it” and a full audit/reporting product.
