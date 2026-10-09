# Week Plan Print Templates

Status: Backend implemented. `Features/PrintTemplates` ships the `PrintTemplate` aggregate (user- or
group-owned), all eight slices and routes below, the index document, the inline snapshot, golden
files and integration tests, including the `WorkLocation` row kind over
[work-locations.md](work-locations.md). The print sheet and template editor are built too; see
[week-plan-printing.md](../../frontend/analysis/week-plan-printing.md).

## Context

Many families already keep a paper week plan on the fridge: a landscape sheet
with one column per day and one row per kind of information — dinner, who
drops off and picks up, which guardian is away, each child's activities, a
child's chores as tick boxes, appointments, and which bin goes out. A guardian
wants Buddy to print that sheet from data Buddy already holds, instead of
copying it out by hand every week.

What was asked for specifically:

1. A printable week plan, on **A3 or A4**, always **landscape**.
2. The guardian picks a **start date**, and the sheet runs from that day — pick
   a Sunday and Sunday is the first column.
3. The layout and data selection (which calendars, which children, which rows)
   are **saved as a configuration**, so the next print only needs a new start
   date.

Almost every row on such a sheet maps onto an existing read surface:

| Paper row | Existing source |
|---|---|
| Dinner | `ListMealPlan` / `ListMealPlanForGroup`, `MealSlot.Dinner` |
| Drop-off / pick-up | `ListPickupSchedule`, `PickupSlot.DropOff` / `PickUp` |
| "Dad at the office", "Mum in Randers" | `ListWorkDays` from the new guardian work locations feature ([work-locations.md](work-locations.md)), shown as a mark |
| A child's activities | `ListOccurrences`, filtered by `AssignedTo` |
| A child's chores | `ListOccurrences`, `Kind = Task`, filtered by `AssignedTo` |
| Appointments / other | `ListOccurrences` on one or more calendars |
| Bins | A recurring calendar item, or left blank for handwriting |

Apart from work locations, which get their own feature in
[work-locations.md](work-locations.md), the only genuinely new data is the
**saved configuration** itself. Fetching
and laying out the week is read-only composition over endpoints that already
exist; see the frontend design,
[week-plan-printing.md](../../frontend/analysis/week-plan-printing.md).

## Question 1: a new feature, or settings on an existing aggregate?

**Decision: a new feature, `Features/PrintTemplates`, with its own aggregate,
`PrintTemplate`.**

- A template references several features at once — calendars, children, the
  meal plan, pickup schedules. Hanging it off any one of them (`Calendar`,
  `MealPlan`, `User`) would make that aggregate own configuration about the
  others.
- A household plausibly wants more than one template ("school week", "holiday
  week", one per child for a child's own wall). That rules out a single
  settings field on `User` or `Group`.
- It keeps every existing feature untouched, the same way `Medicines`,
  `Mealplans` and `Pickups` each left `Calendars` alone
  ([pickup-schedules.md, Question 1](pickup-schedules.md#question-1-extend-calendars-or-a-new-feature)).

## Question 2: who owns a template?

**Decision: user-owned or group-owned, mirroring `Calendar`'s two creation
events.**

The motivating sheet is shared by both guardians, so a template has to be
editable by more than the guardian who created it. `Calendar` already solves
"owned by a person, or by a group" with two sibling creation events —
`CalendarCreated(…, UserId OwnerId, …)` and
`CalendarCreatedForGroup(…, GroupId OwnerId, …)`
([CalendarEvents.cs](../../../src/backend/buddy/Features/Calendars/Types/CalendarEvents.cs)) —
folded into the aggregate-only `CalendarOwner` union. `PrintTemplate` follows
exactly that shape:

```
PrintTemplateCreated(PrintTemplateId, UserId OwnerId, string Name, UserId CreatedBy, DateTimeOffset OccurredAt)
PrintTemplateCreatedForGroup(PrintTemplateId, GroupId OwnerId, string Name, UserId CreatedBy, DateTimeOffset OccurredAt)
```

The union lives on the aggregate only (`PrintTemplateOwner.User` /
`PrintTemplateOwner.Group`), never inside a persisted event — see Question 3
for why that matters.

Transferring a user-owned template to a group (the `TransferCalendarToGroup`
equivalent) is not part of v1; see open questions.

## Question 3: modelling rows — a flat kind discriminator, not a union

**Decision: one flat `PrintTemplateRow` record with a `PrintRowKind` enum and
per-kind optional fields.**

Rows are embedded in the `PrintTemplateRowsReplaced` event, which Marten
persists and replays as JSON. A closed union of row cases would hit the same
failure already found and documented for pickup assignments: System.Text.Json
can't tell structurally similar union cases apart on deserialization
([pickup-schedules.md, Question 3](pickup-schedules.md#question-3-modeling-who--a-flat-kind-discriminator-not-a-union)).
The flat record is the established fix:

```
public enum PrintRowKind { Meal, Pickup, WorkLocation, CalendarMarker, CalendarEvents, TaskChecklist, Blank }

PrintTemplateRow(
    PrintRowKind Kind,
    string Label,                              // printed row header, e.g. "Signes aktiviteter"
    int HeightWeight,                          // 1–5, relative share of the sheet's height
    UserId? ChildId,                           // Meal (family scope), Pickup
    GroupId? MealGroupId,                      // Meal (group-shared plan scope)
    MealSlot? MealSlot,                        // Meal
    UserId? GuardianId,                        // WorkLocation
    WorkLocationId? WorkLocationId,            // WorkLocation (optional: mark only this location)
    IReadOnlyList<CalendarId>? CalendarIds,    // CalendarMarker, CalendarEvents, TaskChecklist
    UserId? AssignedToId,                      // CalendarEvents, TaskChecklist (optional filter)
    string? TitleFilter,                       // CalendarMarker, CalendarEvents (optional, case-insensitive contains)
    int? MaxItems,                             // CalendarEvents, TaskChecklist; overflow shows "+N"
    bool ShowTime,                             // CalendarEvents
    bool ShowAssignee)                         // CalendarEvents
```

What each kind needs, enforced by the validator at write time (the same
"validate the fields this kind needs" rule `AssignPickupValidator` applies with
`.When(x => x.Kind == …)`):

| Kind | Required | Optional | Renders as |
|---|---|---|---|
| `Meal` | exactly one of `ChildId` / `MealGroupId`; `MealSlot` | — | the meal name |
| `Pickup` | `ChildId` | — | a diagonally split cell, drop-off above, pick-up below |
| `WorkLocation` | `GuardianId` | `WorkLocationId` | with `WorkLocationId`, a mark (✕) on each day the guardian is at that location; without it, the day's location icon and name in the guardian's color |
| `CalendarMarker` | `CalendarIds` (1+) | `TitleFilter` | a mark (✕) on any day with a matching occurrence |
| `CalendarEvents` | `CalendarIds` (1+) | `AssignedToId`, `TitleFilter`, `MaxItems`, `ShowTime`, `ShowAssignee` | a list of occurrences |
| `TaskChecklist` | `CalendarIds` (1+) | `AssignedToId`, `MaxItems` | tasks as empty tick boxes |
| `Blank` | — | — | an empty cell for handwriting |

Fields a kind doesn't use must be null/false/empty; the validator rejects
stray values rather than silently dropping them, so stored rows always say
exactly what they mean.

## Domain model

### `PrintTemplate` (new aggregate, one stream per template)

```
PrintTemplate(
    PrintTemplateId Id,
    PrintTemplateOwner Owner,                  // aggregate-only union, see Question 2
    string Name,
    PaperSize PaperSize,                       // A4 | A3; orientation is always landscape and not stored
    DayOfWeek DefaultStartWeekday,             // pre-fills the start-date picker
    bool ShowWeekNumber,
    IReadOnlyList<PrintTemplateRow> Rows,      // ordered top to bottom
    IReadOnlyList<GuardianColor> GuardianColors,
    bool IsDeleted)
{
    IReadOnlyList<BabysitterColor> BabysitterColors   // init property, defaults to []
}

public enum PaperSize { A4, A3 }

GuardianColor(UserId GuardianId, Color Color)
BabysitterColor(UserId GuardianId, BabysitterId BabysitterId, Color Color)
```

- **Orientation is not a field.** Landscape is a product rule, not a choice;
  storing it would invite a value nobody can set.
- **The start date is not stored.** A template is reusable precisely because
  it holds everything *except* the date. `DefaultStartWeekday` only decides
  what the date picker suggests.
- **Day count is fixed at 7 in v1.** A week plan is a week; a configurable
  span is deferred (see open questions) in line with this codebase's
  avoid-speculative-generalization stance.
- **`GuardianColors` is a list, not a dictionary.** It colors guardian names
  in `Pickup` and `CalendarEvents` cells (the paper sheet uses blue and red).
  A list of small records serializes predictably; a dictionary keyed by a
  strongly typed id is the kind of JSON shape this codebase has already been
  bitten by. Guardians without an entry print in the default ink color.
  `Color` reuses the existing calendar `Color` value type
  ([Color.cs](../../../src/backend/buddy/Features/Calendars/Types/Color.cs)).
- **`BabysitterColors` colors babysitter names in `Pickup` cells**, added
  after the first release. A babysitter is keyed by the
  `(GuardianId, BabysitterId)` pair that `PickupAssignee.Babysitter` carries,
  since babysitter ids are only unique within one guardian's list. It is an
  init property rather than a positional parameter so snapshots stored before
  it existed still deserialize to an empty list instead of `null`.

### Events

Following the existing `Before`/`After` convention for mutations:

```
PrintTemplateCreated(PrintTemplateId, UserId OwnerId, string Name, UserId CreatedBy, DateTimeOffset OccurredAt)
PrintTemplateCreatedForGroup(PrintTemplateId, GroupId OwnerId, string Name, UserId CreatedBy, DateTimeOffset OccurredAt)

PrintTemplateRenamed(PrintTemplateId, string Before, string After, UserId ModifiedBy, DateTimeOffset OccurredAt)

PrintTemplateLayoutChanged(PrintTemplateId,
    PaperSize PaperSizeBefore, PaperSize PaperSizeAfter,
    DayOfWeek StartWeekdayBefore, DayOfWeek StartWeekdayAfter,
    bool ShowWeekNumberBefore, bool ShowWeekNumberAfter,
    UserId ModifiedBy, DateTimeOffset OccurredAt)

PrintTemplateRowsReplaced(PrintTemplateId,
    IReadOnlyList<PrintTemplateRow> Before, IReadOnlyList<PrintTemplateRow> After,
    UserId ModifiedBy, DateTimeOffset OccurredAt)

PrintTemplateGuardianColorsReplaced(PrintTemplateId,
    IReadOnlyList<GuardianColor> Before, IReadOnlyList<GuardianColor> After,
    UserId ModifiedBy, DateTimeOffset OccurredAt)

PrintTemplateBabysitterColorsReplaced(PrintTemplateId,
    IReadOnlyList<BabysitterColor> Before, IReadOnlyList<BabysitterColor> After,
    UserId ModifiedBy, DateTimeOffset OccurredAt)

PrintTemplateDeleted(PrintTemplateId, UserId DeletedBy, DateTimeOffset OccurredAt)
```

**Rows are replaced wholesale, not patched.** The editor works on the whole
list (add, remove, reorder, edit), and templates are small — a dozen rows at
most. One "replace" event keeps every edit atomic and makes reordering
trivial, at the cost of a larger payload; per-row add/move/remove events would
multiply slices and event types for no reader that needs that granularity.

New event types get golden-file coverage in
`buddy.IntegrationTests/EventShapeTests`, like every other persisted event.

### Rehydration

`PrintTemplate.Rehydrate(events)` follows `Calendar.Rehydrate`: either
creation event seeds the record with defaults (`PaperSize.A4`,
`DayOfWeek.Monday`, `ShowWeekNumber = true`, empty `Rows` and
`GuardianColors`), each mutation event overwrites its fields with `After`, and
`PrintTemplateDeleted` sets `IsDeleted`. A deleted template is treated as
missing by every handler.

## Read model

Listing "templates I can use" needs an index, since Marten streams are
addressed by aggregate id:

```
PrintTemplateIndexDocument(Guid Id, Guid? OwnerUserId, Guid? OwnerGroupId, string Name, bool IsDeleted)
```

Written inline in the same `SaveChangesAsync` as the event append — on
creation, rename, and deletion — the maintained-inline-projection pattern used
by every other feature (`PickupScheduleIndexDocument`,
`MealPlanIndexDocument`). `ListPrintTemplates` queries it for
`OwnerUserId == caller` plus `OwnerGroupId IN (caller's groups)`.

## Authorization

| Tier | Who | Actions |
|---|---|---|
| **Manage** | User-owned: the owner. Group-owned: any group member who is not a child account | View, edit, delete |
| — | Everyone else, including children in the owning group | `NotFound` |

- Children can be group members (`AddChildToGroup`), so group membership alone
  is not enough. The existing `ChildVisibility.IsChildAsync` check excludes
  them. Printing is a guardian activity; there is no child tier in v1.
- There is deliberately no View-only tier. Unlike calendars, a template has no
  audience that should read it without being allowed to adjust it. If group
  owners later want to lock templates down, a `PrintTemplatePermissionPolicy`
  on `Group` — the same shape as `UpdateMealplanPermissionPolicy` — is the
  additive path.

**A template grants no access to the data it references.** Holding a template
that names a calendar does not let anyone read that calendar. At print time
the frontend fetches every row through the existing endpoints, which run their
own authorization (`CalendarAuthorization.CheckView`,
`PickupAuthorization`, `MealplanAuthorization`, `WorkLocationAuthorization`) for whoever is printing. A row
whose source that guardian can't read prints a short "not available" note
instead of failing the whole sheet.

Write-time checks are about catching mistakes, not granting access. They run
as the guardian saving, and only over references that are **new in that
save**: a group template legitimately holds rows another member added (their
private calendar, their child), and a reference that has gone stale since must
not block every later save:

- every `ChildId` must be a child the caller is an active guardian of
  (`IGuardianLinkEventStore.FindActiveLinkAsync`);
- every `CalendarId` must be one the caller can currently view;
- every `GuardianColor.GuardianId`, and every `WorkLocation` row's
  `GuardianId`, must be the caller or an active guardian of at least one
  child the caller guards;
- every `WorkLocationId` must be an active (not archived) location in that
  guardian's work location schedule.

A reference that later goes stale — a calendar deleted, a guardian link
revoked — is left in the stored template (history isn't rewritten) and
surfaces as a "not available" row at print time and a warning in the editor.

## Command slices (`Features/PrintTemplates/`)

| Slice | Tier | Notes |
|---|---|---|
| `CreatePrintTemplate` | — | `Name`, optional `GroupId`. With a group, the caller must be a non-child member. Emits `PrintTemplateCreated` or `PrintTemplateCreatedForGroup` and writes the index row |
| `ListPrintTemplates` | — | Index query: the caller's own templates plus their groups' templates, `IsDeleted == false` |
| `GetPrintTemplate` | Manage | Full template |
| `RenamePrintTemplate` | Manage | Emits `PrintTemplateRenamed`; updates the index |
| `UpdatePrintTemplateLayout` | Manage | `PaperSize`, `DefaultStartWeekday`, `ShowWeekNumber`. No-op when unchanged (`Success`, no event) |
| `ReplacePrintTemplateRows` | Manage | Full ordered row list; validated per Question 3 plus the write-time checks above. No-op when equal |
| `ReplacePrintTemplateGuardianColors` | Manage | Full list; one entry per guardian at most |
| `ReplacePrintTemplateBabysitterColors` | Manage | Full list; one entry per babysitter at most. A new entry must be an active babysitter on the list of the caller or a co-guardian |
| `DeletePrintTemplate` | Manage | Emits `PrintTemplateDeleted`; marks the index row deleted |

There is no "render" or "print" slice. Rendering is read-only composition over
existing endpoints and lives in the frontend.

### Validation limits

Structural rules live in each slice's validator (`PrintTemplateRules`,
`PrintTemplateRowValidator`); the reference checks above need the database and
run in the handler (`PrintTemplateReferenceChecks`), as
[validation-rules.md](validation-rules.md) prescribes:

| Field | Rule |
|---|---|
| `Name` | required, trimmed, 1–80 characters |
| `Rows` | 1–12 rows |
| `Row.Label` | trimmed, 0–40 characters (empty is allowed for `Blank`) |
| `Row.HeightWeight` | 1–5 |
| `Row.CalendarIds` | 1–10 distinct ids where required |
| `Row.TitleFilter` | 1–60 characters when present |
| `Row.MaxItems` | 1–8 when present |
| `GuardianColors` | at most one entry per guardian, at most 12 entries, each color trimmed, 1–32 characters |
| `BabysitterColors` | at most one entry per `(GuardianId, BabysitterId)`, at most 24 entries, each color trimmed, 1–32 characters |
| `Row.CalendarIds` entries | a null id is rejected with `400`, not stored |

Validation error keys use the camelCase JSON path (`rows[2].calendarIds`), so the editor can
map them to fields. A `Blank` row may omit `label` entirely; the endpoint stores it as `""`.

## Routes

```
POST   /print-templates                          CreatePrintTemplate
GET    /print-templates                          ListPrintTemplates
GET    /print-templates/{templateId}             GetPrintTemplate
PATCH  /print-templates/{templateId}/name        RenamePrintTemplate
PATCH  /print-templates/{templateId}/layout      UpdatePrintTemplateLayout
PUT    /print-templates/{templateId}/rows        ReplacePrintTemplateRows
PUT    /print-templates/{templateId}/colors      ReplacePrintTemplateGuardianColors
PUT    /print-templates/{templateId}/babysitter-colors  ReplacePrintTemplateBabysitterColors
DELETE /print-templates/{templateId}             DeletePrintTemplate
```

`PUT` for rows and colors because each replaces a whole collection; `PATCH`
for name and layout, matching `UpdateCalendarIcon` / `UpdateItemDetails`.
Status codes follow [http-status-codes.md](../http-status-codes.md):
`NotFound` for anything outside the caller's Manage tier, `BadRequest` with an
`ErrorEnvelope` for validation failures.

## Failure and edge-case behavior

| Case | Behavior |
|---|---|
| A referenced calendar is deleted | The template keeps the id. At print time the row shows "not available"; the editor flags the row so a guardian can fix it |
| The printing guardian can't view a referenced calendar (e.g. a co-guardian's private calendar) | That row shows "not available" for this guardian only; other rows print normally |
| A referenced child's guardian link is revoked for the printing guardian | Same as above — the pickup/meal endpoints return `NotFound`, the row degrades |
| A referenced work location is archived | The row keeps printing its marks (archived locations still resolve); the editor flags the row |
| A guardian named in `GuardianColors` is no longer linked | The entry is harmless and unused; the editor hides it |
| A babysitter named in `BabysitterColors` is archived, or its guardian is no longer linked | The entry stays and doesn't block later saves (only new entries are checked); the editor hides it. Pickups already assigned to an archived babysitter keep printing in its color |
| Two guardians edit rows at nearly the same time | Each write is an expected-version append (`StreamVersionTracker`), so a true race gives the second writer `409 concurrency_conflict` and nothing is lost; saves that don't overlap simply replace each other in order |
| A template is deleted twice | The second delete is `404`: a deleted template is treated as missing, the same rule `DeleteCalendar` follows |
| A child account | Can't create (`403`), lists nothing (an empty list, even for its group's templates) and gets `404` for any template |
| A user who owns templates deletes their account | User-owned templates become unreachable with the account, as user-owned calendars do. Group-owned templates are unaffected |
| A group is deleted | Its templates become unreachable; the index rows are filtered out by group membership |
| The start week crosses a DST change | No effect on the template. Occurrences already arrive resolved by each calendar's `TimeZoneId` |

## Decisions made

| Question | Decision |
|---|---|
| New feature or extend an existing one | New feature, `Features/PrintTemplates` |
| Ownership | User-owned or group-owned, via two sibling creation events, mirroring `Calendar` |
| Row modelling | Flat `PrintTemplateRow` with a `PrintRowKind` enum — no union inside persisted events |
| Paper | `PaperSize { A4, A3 }`; landscape always, not stored |
| Start date | Not stored; chosen per print. `DefaultStartWeekday` only pre-fills the picker |
| Day count | Fixed at 7 in v1 |
| Row edits | Whole-list replacement in one event |
| Who can edit | The owner, or any non-child member of the owning group |
| Does a template grant data access | No — every row is fetched through existing, separately authorized endpoints |
| Server-side rendering endpoint | None in v1; composition happens in the frontend |
| `MealGroupId` write-time check | The caller must be a non-child member of that group (added during implementation, alongside the checks listed under Authorization) |
| `AssignedToId` write-time check | None: a wrong assignee only filters a row down to nothing, and print-time authorization already covers access |
| Write-time reference checks | Only over references that are new in the save, so co-editing a group template and keeping a stale row don't block later saves |
| Wire format for enums | Numeric ordinals (`PaperSize`, `PrintRowKind`, `DayOfWeek` with `0` = Sunday); there is no string enum converter |
| Concurrent edits | Expected-version appends; a true race is `409 concurrency_conflict`, not last-write-wins |
| Guardian "away"/office rows | A dedicated `WorkLocation` row kind over [work-locations.md](work-locations.md), not a `CalendarMarker` with a title filter |

## Remaining open questions

- **Transferring a user-owned template to a group.** A
  `TransferPrintTemplateToGroup` slice mirroring `TransferCalendarToGroup` is
  additive if guardians start with personal templates and later want to share.
- **Configurable span (e.g. 14 days).** Deferred until asked for; the layout
  has to stay legible at A4 first.
- **Rows for other features.** Medicine doses and goal-post progress are
  natural future `PrintRowKind` values; each is an additive enum value plus
  validator and renderer support.
- **Server-side PDF.** If browser print output proves inconsistent, a
  rendering slice (headless browser over the same print route) can be added
  without changing the template model.
- **Starter template.** Whether a household gets a pre-built template on first
  visit (created lazily, like `MealPlanCreated`) or starts empty with a "start
  from example" button. Leaning towards the button, since lazy creation would
  write data nobody asked for.
