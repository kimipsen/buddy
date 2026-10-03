using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// No ChildId: a Meal is shared by every child in the family it was created for (see
// MealFamilyResolution), not owned by a single child, so "whose meal is this" isn't a question
// the aggregate itself answers.
public sealed record Meal(
    MealId Id,
    UserId CreatedBy,
    string Name,
    string Description,
    Icon Icon,
    Color Color,
    bool IsArchived,
    ImmutableDictionary<UserId, MealRating> Ratings,
    UserId LastModifiedBy)
{
    public static Meal? Rehydrate(IEnumerable<MealEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static Meal Replay(IEnumerable<MealEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so MealSnapshotProjection can
    // drive the same logic one Marten-delivered event at a time instead of duplicating this switch.
    // Deliberately not named Apply/Create -- those names are a convention JasperFx's projection
    // source generator scans for on any type used as a projection document, and Meal is that
    // document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static Meal Start(MealEvent @event) => @event switch
    {
        MealCreated created => new Meal(
            created.Id,
            created.CreatedBy,
            created.Name,
            created.Description,
            created.Icon,
            created.Color,
            IsArchived: false,
            ImmutableDictionary<UserId, MealRating>.Empty,
            created.CreatedBy),
        _ => throw EventReplay.NotAStartEvent(nameof(Meal), @event.EventType)
    };

    public static Meal Advance(Meal meal, MealEvent @event) => @event switch
    {
        MealDetailsUpdated updated => meal with
        {
            Name = updated.After.Name,
            Description = updated.After.Description,
            Icon = updated.After.Icon,
            Color = updated.After.Color,
            LastModifiedBy = updated.ModifiedBy
        },
        MealArchived archived => meal with { IsArchived = true, LastModifiedBy = archived.ModifiedBy },
        // Keyed by which child rated it -- each sibling has their own opinion of a shared
        // meal, so one Meal can hold one rating per child rather than a single value.
        MealRated rated => meal with
        {
            Ratings = meal.Ratings.SetItem(rated.ChildId, rated.Rating),
            LastModifiedBy = rated.ChildId
        },
        MealCreated => throw EventReplay.AlreadyStarted(nameof(Meal), @event.EventType)
    };
}
