# Pickup planning and daily views

Status: Implemented

Buddy exposes pickup and drop-off assignments in three frontend contexts: a
guardian edits a child's rolling seven-day plan, the guardian dashboard
summarizes today's assignments across children, and the child home screen shows
the child's own assignments read-only. The backend lifecycle and permission
model are documented in the [pickups flow](../../backend/pickups/flow.md); this
page records the implemented Angular behavior.

## Guardian planner

`/guardian/pickup` renders `GuardianPickup`, whose main workflow is implemented
by
[`ManagePickups`](../../../src/frontend/buddy/src/app/features/guardian/pickup/manage-pickups/manage-pickups.ts).
It loads the signed-in guardian's children, selects the first child by default,
and displays today plus the next six days. Each day has `DropOff` and `PickUp`
columns.

Changing the selected child loads two resources in parallel:

- `GuardiansService.listChildGuardians(childId)` supplies guardian names for
  assignment choices and summaries.
- `PickupsService.listSchedule(childId, from, to)` supplies the sparse pickup
  occurrences for the seven-day window.

Occurrences are indexed client-side by `date|slot`. A missing key renders as
not planned; it is not the same as a present `SelfEscort` occurrence. Sibling
choices come from the guardian's loaded children with the selected child
removed. The backend remains responsible for proving that the selected child
actually shares an active guardian with the scheduled child.

## Inline cell editing

[`PickupCell`](../../../src/frontend/buddy/src/app/features/guardian/pickup/pickup-cell/pickup-cell.ts)
renders each grid cell in one of three states:

- **Unplanned:** a button opens an inline editor.
- **Assigned:** a compact summary shows the assignee and optional time; selecting
  it opens the same editor with current values.
- **Editing/saving:** the assignee selector, conditional fields, optional time,
  and notes appear in place. The cell is disabled while its request is active.

The assignee kind controls the required input:

| Kind | Frontend fields |
| --- | --- |
| Guardian | One guardian from `listChildGuardians`; selection is required. |
| Self-escort | No assignee fields. |
| Sibling | One other loaded child; selection is required. |
| Playdate | Required host name with optional location and contact information. |

The save button is disabled until the kind-specific required value exists.
Times are edited as `HH:mm`, sent as `HH:mm:00`, and displayed through the
locale-aware time pipe. Notes and optional playdate fields are trimmed, with
empty values sent as `null`.

Saving calls `PickupsService.assignPickup()` and replaces the one local cell
with the returned occurrence. Clearing calls `clearPickup()` and removes the
local key. Neither operation refetches the full schedule. Load failures and
update failures use translated error keys; there is no optimistic update before
the server succeeds.

## Today summaries

[`PickupToday`](../../../src/frontend/buddy/src/app/features/guardian/pickup-today/pickup-today.ts)
is the guardian dashboard summary. It loads today's schedule and guardian list
for every linked child in parallel, flattens the results, and sorts by slot.
The row includes the child name when the guardian has multiple children and
links to the full pickup planner.

[`ChildHome`](../../../src/frontend/buddy/src/app/features/child/home/home.ts)
loads the signed-in child's schedule for today. It resolves guardian and
sibling IDs against the child's relationship lists and renders the assignment
alongside meals, medicine doses, and tasks. The child view has no assignment or
clear action, matching the backend's read-only child access tier.

## API contract

[`PickupsService`](../../../src/frontend/buddy/src/app/core/pickups.service.ts)
wraps the three backend routes with Promise-returning `HttpClient` calls:

| Method | Request |
| --- | --- |
| `listSchedule` | `GET /pickups/children/{childId}/schedule?from=YYYY-MM-DD&to=YYYY-MM-DD` |
| `assignPickup` | `PUT /pickups/children/{childId}/assignments?date=YYYY-MM-DD&slot={0|1}` |
| `clearPickup` | `DELETE /pickups/children/{childId}/assignments?date=YYYY-MM-DD&slot={0|1}` |

The TypeScript numeric unions deliberately match backend enum ordinals:
`0 = DropOff`, `1 = PickUp`; and `0 = Guardian`, `1 = SelfEscort`,
`2 = Sibling`, `3 = Playdate`. The API does not register a string enum
converter, so changing either side's order is a contract change.

## Responsive behavior and localization

The planner table has a minimum width and sits inside a horizontal overflow
container, so narrow screens scroll the seven-day grid instead of compressing
cell editors beyond use. Static UI text is translated in the English and Danish
`pickup` dictionaries. Date labels use the current `TranslationService`
language, and times use the application's locale-aware display pipe.

## Current limitations

- The planner always shows today plus six days; there is no previous/next week
  navigation, recurring template, bulk assignment, or copy-day action.
- Concurrent edits are last-write-wins. The frontend has no version token or
  conflict UI.
- The selected child is not encoded in the URL, and a page reload returns to the
  first child.
- Guardian and sibling display names depend on separately loaded relationship
  lists; removed or unavailable relationships can leave an ID without a name.
- Several inline editor controls rely on placeholders or surrounding context
  instead of explicit associated labels. Keyboard and screen-reader behavior
  should be audited before treating the editor as fully accessible.
- The wide grid is scrollable on mobile, but the fixed-width inline editor has
  not been documented as tested across all supported viewport sizes.

These are frontend follow-ups, not changes to the pickup aggregate or its
current authorization model.
