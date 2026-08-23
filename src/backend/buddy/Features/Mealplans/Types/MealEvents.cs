using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public union MealEvent(
    MealCreated,
    MealDetailsUpdated,
    MealArchived,
    MealRated
)
{
    public static MealEvent FromPayload(object payload) => payload switch
    {
        MealCreated e => e,
        MealDetailsUpdated e => e,
        MealArchived e => e,
        MealRated e => e,
        _ => throw new ArgumentException($"Unknown meal event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        MealCreated => nameof(MealCreated),
        MealDetailsUpdated => nameof(MealDetailsUpdated),
        MealArchived => nameof(MealArchived),
        MealRated => nameof(MealRated),
    };
}

// ChildId records which child the creating guardian was acting on behalf of -- needed by
// MartenMealEventStore.CreateAsync to seed the meal's first index row, but not projected onto the
// Meal aggregate itself, since sharing (see MealFamilyResolution) makes "whose meal is this" a
// read-time question, not aggregate state.
public sealed record MealCreated(
    MealId Id,
    UserId ChildId,
    UserId CreatedBy,
    string Name,
    string? Description,
    Icon Icon,
    Color Color,
    DateTimeOffset OccurredAt);

public sealed record MealDetailsUpdated(MealId Id, MealDetails Before, MealDetails After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// Soft "delete" -- same shape as MedicineScheduleStopped. An archived meal can no longer be newly
// assigned to a plan slot, but existing plan entries and every child's rating remain fully readable.
public sealed record MealArchived(MealId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);

// The only event a child, rather than a guardian, ever appends (see MealplanAuthorization).
// ChildId is both the rating's subject and its actor -- only that child can ever rate for
// themself, so there's no separate "RatedBy" to carry. No separate "unrate" event -- a changed
// opinion simply appends another MealRated with a new After.
public sealed record MealRated(MealId Id, UserId ChildId, MealRating? Before, MealRating After, DateTimeOffset OccurredAt);
