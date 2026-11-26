using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public union FlashCardSetEvent(
    FlashCardSetCreated,
    FlashCardSetUpdated,
    FlashCardAdded,
    FlashCardUpdated,
    FlashCardRemoved,
    FlashCardSetDeleted
)
{
    public static FlashCardSetEvent FromPayload(object payload) => payload switch
    {
        FlashCardSetCreated e => e,
        FlashCardSetUpdated e => e,
        FlashCardAdded e => e,
        FlashCardUpdated e => e,
        FlashCardRemoved e => e,
        FlashCardSetDeleted e => e,
        _ => throw new ArgumentException($"Unknown flash card set event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        FlashCardSetCreated => nameof(FlashCardSetCreated),
        FlashCardSetUpdated => nameof(FlashCardSetUpdated),
        FlashCardAdded => nameof(FlashCardAdded),
        FlashCardUpdated => nameof(FlashCardUpdated),
        FlashCardRemoved => nameof(FlashCardRemoved),
        FlashCardSetDeleted => nameof(FlashCardSetDeleted),
    };
}

// Math is set for a Math set and null for a Text set.
public sealed record FlashCardSetCreated(FlashCardSetId Id, UserId OwnerId, string Name, FlashCardSetKind Kind, MathRule? Math, DateTimeOffset OccurredAt);

// A null Math keeps the set's current rule (a rename).
public sealed record FlashCardSetUpdated(FlashCardSetId Id, string Name, MathRule? Math, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record FlashCardAdded(FlashCardSetId Id, FlashCard Card, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record FlashCardUpdated(FlashCardSetId Id, FlashCardId CardId, string Question, string Answer, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record FlashCardRemoved(FlashCardSetId Id, FlashCardId CardId, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record FlashCardSetDeleted(FlashCardSetId Id, UserId DeletedBy, DateTimeOffset OccurredAt);
