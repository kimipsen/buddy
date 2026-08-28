# Backend domain model diagram

A UML-style class diagram of every event-sourced aggregate root in the
backend. This is a companion to
[Aggregate roots and their relationships](aggregate-roots.md), which is the
source of truth for the exact stored references and the reasoning behind
each modeling choice — read that first if you're changing the model. This
doc just renders the same aggregates as classes with their key fields.

There's no shared `AggregateRoot` base type. Each aggregate is a sealed
record in a `Features/<Feature>/Types/` folder with a static `Rehydrate`
factory that folds its event stream into current state. Fields below are
simplified from the real immutable collection types for readability — for
example `MemberRoles` stands in for
`ImmutableDictionary<UserId, GroupRole>`.

```mermaid
classDiagram
    class User {
        +UserId Id
        +Email Email
        +Name Name
        +TimeZoneId TimeZoneId
        +Language Language
        +bool IsDeleted
    }

    class GuardianLink {
        +GuardianLinkId Id
        +UserId ChildId
        +UserId GuardianId
        +GuardianKind Kind
        +bool IsRevoked
    }

    class Group {
        +GroupId Id
        +string Name
        +MemberRoles Members
        +PermissionPolicies Policies
        +bool IsDeleted
    }

    class Calendar {
        +CalendarId Id
        +string Name
        +CalendarOwner Owner
        +MemberRoles Members
        +IcalTokens Tokens
        +bool IsDeleted
    }

    class CalendarItem {
        +CalendarItemId Id
        +CalendarId CalendarId
        +CalendarItemKind Kind
        +string Title
        +CompletionLog CompletionLog
        +UserId AssignedTo
        +bool IsDeleted
    }

    class MealPlan {
        +MealPlanId Id
        +SlotAssignments Assignments
        +IcalTokens Tokens
        +GroupId SharedWithGroupId
    }

    class Meal {
        +MealId Id
        +UserId CreatedBy
        +string Name
        +bool IsArchived
        +Ratings Ratings
    }

    class MedicineSchedule {
        +MedicineId Id
        +UserId ChildId
        +string Name
        +string Dosage
        +DoseLog DoseLog
        +bool IsStopped
    }

    class MedicineSharing {
        +MedicineSharingId Id
        +UserId ChildId
        +GroupId SharedWithGroupId
    }

    class PickupSchedule {
        +PickupScheduleId Id
        +UserId ChildId
        +SlotAssignments Assignments
    }

    class TaskTemplate {
        +TaskTemplateId Id
        +UserId CreatedBy
        +string Name
        +Subtasks Subtasks
        +bool IsArchived
    }

    class ChildProgress {
        +ProgressId Id
        +UserId ChildId
        +int TotalStars
        +AwardedOccurrences AwardedOccurrences
    }

    GuardianLink --> User : guardianId, childId
    Group --> User : members
    Calendar --> User : owner, user-owned
    Calendar --> Group : owner, group-owned
    Calendar --> User : members
    CalendarItem --> Calendar : calendarId
    CalendarItem --> User : createdBy, lastModifiedBy
    CalendarItem ..> TaskTemplate : taskTemplateId
    MealPlan --> Group : sharedWithGroupId
    MealPlan --> Meal : assignments
    MealPlan --> User : assignedBy
    Meal --> User : createdBy, ratings
    MedicineSchedule --> User : childId, createdBy
    MedicineSharing --> User : childId
    MedicineSharing --> Group : sharedWithGroupId
    PickupSchedule --> User : childId, assignments
    TaskTemplate --> User : createdBy, lastModifiedBy
    ChildProgress --> User : childId
    ChildProgress ..> CalendarItem : awardedOccurrences
```

Solid arrows are a stored reference (the tail aggregate holds the head
aggregate's id). Dashed arrows are relationships resolved at read time
instead of stored.

A few modeling choices worth knowing before touching this: a child isn't its
own type — it's a `User` pointed at by a `GuardianLink`. Family sharing is
computed, not stored — `Meal` and `MealPlan` carry no child id at all; which
family a meal plan belongs to is resolved at read time from the
`GuardianLink` graph. `MedicineSchedule` and `PickupSchedule` make the
opposite call and store `ChildId` directly, since each belongs to exactly
one child.
