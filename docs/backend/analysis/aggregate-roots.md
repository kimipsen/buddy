# Aggregate Roots and Their Relationships

This is a map of every event-sourced aggregate root in the backend and how
they reference each other. There is no shared `AggregateRoot` base type or
marker interface — each aggregate is a `sealed record` in a
`Features/<Feature>/Types/` folder with a
`static X? Rehydrate(IEnumerable<XEvent> events)` factory that folds its
event stream into current state. The twelve below were found by grepping for
that convention.

An arrow means the aggregate at the tail stores the id of the aggregate at
the head. Dashed arrows are relationships that are *computed* at read time
instead of stored on the aggregate.

```mermaid
flowchart TB
    subgraph Identity["Identity"]
        User["User\nUserId(Guid)"]
        GuardianLink["GuardianLink\nGuardianLinkId(Guid)"]
    end

    subgraph Access["Access & scheduling"]
        Group["Group\nGroupId(Guid)"]
        Calendar["Calendar\nCalendarId(Guid)"]
        CalendarItem["CalendarItem\nCalendarItemId(Guid)"]
    end

    subgraph FamilyContent["Family content"]
        MealPlan["MealPlan\nMealPlanId(Guid)"]
        Meal["Meal\nMealId(Guid)"]
        MedicineSchedule["MedicineSchedule\nMedicineId(Guid)"]
        MedicineSharing["MedicineSharing\nMedicineSharingId(Guid)"]
        PickupSchedule["PickupSchedule\nPickupScheduleId(Guid)"]
        TaskTemplate["TaskTemplate\nTaskTemplateId(Guid)"]
        ChildProgress["ChildProgress\nProgressId(Guid)"]
    end

    GuardianLink -- "guardianId, childId : UserId" --> User

    Group -- "owner, members : UserId" --> User

    Calendar -- "owner (if User-owned)" --> User
    Calendar -- "owner (if Group-owned)" --> Group
    Calendar -- "members : UserId" --> User
    CalendarItem -- "calendarId" --> Calendar
    CalendarItem -- "createdBy / lastModifiedBy" --> User
    CalendarItem -- "taskTemplateId (raw Guid, tasks scheduled from a template)" --> TaskTemplate

    MealPlan -- "sharedWithGroupId" --> Group
    MealPlan -- "assignments : MealId" --> Meal
    MealPlan -- "assignedBy" --> User
    Meal -- "createdBy / lastModifiedBy / ratings" --> User
    MedicineSchedule -- "childId (stored), createdBy" --> User
    MedicineSharing -- "childId, sharedWithGroupId" --> User
    MedicineSharing -- "sharedWithGroupId" --> Group
    PickupSchedule -- "childId (stored)" --> User
    PickupSchedule -- "assignments: guardianId / siblingChildId / assignedBy" --> User
    TaskTemplate -- "createdBy / lastModifiedBy" --> User
    ChildProgress -- "childId (Id == ChildId, no index needed)" --> User
    ChildProgress -. "awardedOccurrences : CalendarItemId" .-> CalendarItem

    GuardianLink -. "family resolved at read time" .-> MealPlan
    GuardianLink -. "family resolved at read time" .-> Meal
    GuardianLink -. "sibling relationship validated at write time" .-> PickupSchedule
    GuardianLink -. "ownership indexed at write time, not resolved" .-> TaskTemplate
```

## Notes

- **"Child" isn't its own aggregate.** A child is a `User` like any other.
  The only thing marking it as a child is a `GuardianLink` pointing
  `guardianId -> childId`, both plain `UserId`s. There is no `Child` type
  anywhere in the domain.
- **Family sharing is computed, not stored.** `Meal` and `MealPlan`
  deliberately carry no `ChildId` — a meal plan is shared across siblings,
  and "which family" is resolved at read time from the `GuardianLink` graph
  via `MealFamilyResolution` (see [mealplans.md](mealplans.md)).
  `MedicineSchedule` and `PickupSchedule` make the opposite choice and store
  `ChildId` directly, because each belongs to exactly one child and isn't
  family-shared — `PickupSchedule` still reaches across to a sibling via a
  plain `UserId` in an assignment, validated (not resolved) against
  `GuardianLink` at write time (see
  [pickup-schedules.md](pickup-schedules.md)).

## Reference

| Aggregate | File | Id type | Stored references |
|---|---|---|---|
| User | `Features/Users/Types/User.cs` | `UserId(Guid)` | — identity root, referenced by every other aggregate |
| GuardianLink | `Features/Guardians/Types/GuardianLink.cs` | `GuardianLinkId(Guid)` | `guardianId`, `childId` → User |
| Group | `Features/Groups/Types/Group.cs` | `GroupId(Guid)` | `members` (keys) → User |
| Calendar | `Features/Calendars/Types/Calendar.cs` | `CalendarId(Guid)` | `owner` → User or Group; `members` → User |
| CalendarItem | `Features/Calendars/Types/CalendarItem.cs` | `CalendarItemId(Guid)` | `calendarId` → Calendar; `createdBy` / `lastModifiedBy` → User |
| MealPlan | `Features/Mealplans/Types/MealPlan.cs` | `MealPlanId(Guid)` | `assignments` → Meal; `sharedWithGroupId` → Group; `assignedBy` → User |
| Meal | `Features/Mealplans/Types/Meal.cs` | `MealId(Guid)` | `createdBy` / `lastModifiedBy` → User; `ratings` (keys) → User |
| MedicineSchedule | `Features/Medicines/Types/MedicineSchedule.cs` | `MedicineId(Guid)` | `childId`, `createdBy` / `lastModifiedBy` → User |
| MedicineSharing | `Features/Medicines/Types/MedicineSharing.cs` | `MedicineSharingId(Guid)` | `childId` → User; `sharedWithGroupId` → Group |
| PickupSchedule | `Features/Pickups/Types/PickupSchedule.cs` | `PickupScheduleId(Guid)` | `childId` → User; assignments' `guardianId` / `siblingChildId` / `assignedBy` → User |
| TaskTemplate | `Features/TaskLibrary/Types/TaskTemplate.cs` | `TaskTemplateId(Guid)` | `createdBy` / `lastModifiedBy` → User; owning `childId` lives in `TaskTemplateIndexDocument`, not the aggregate itself |
| ChildProgress | `Features/Progress/Types/ChildProgress.cs` | `ProgressId(Guid)`, equal to the child's `UserId` | `childId` → User; `awardedOccurrences` → CalendarItem (computed reference, not a foreign-key relationship) |

`CalendarItem` also stores an optional `taskTemplateId` (a raw `Guid`, not `TaskLibrary`'s `TaskTemplateId` type) when it was scheduled from a template — see `Features/Calendars/Types/CalendarItem.cs`.
