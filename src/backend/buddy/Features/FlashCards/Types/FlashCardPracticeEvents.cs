using System.Collections.Immutable;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public union FlashCardPracticeEvent(
    FlashCardPracticeStarted,
    FlashCardSetEnabled,
    FlashCardSetDisabled,
    FlashCardGameFinished
)
{
    public static FlashCardPracticeEvent FromPayload(object payload) => payload switch
    {
        FlashCardPracticeStarted e => e,
        FlashCardSetEnabled e => e,
        FlashCardSetDisabled e => e,
        FlashCardGameFinished e => e,
        _ => throw new ArgumentException($"Unknown flash card practice event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        FlashCardPracticeStarted => nameof(FlashCardPracticeStarted),
        FlashCardSetEnabled => nameof(FlashCardSetEnabled),
        FlashCardSetDisabled => nameof(FlashCardSetDisabled),
        FlashCardGameFinished => nameof(FlashCardGameFinished),
    };
}

public sealed record FlashCardPracticeStarted(FlashCardPracticeId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Full replace of the set's tiers; also how the tiers of an already enabled set are changed.
// SetName is a snapshot for display after the set is renamed or deleted.
public sealed record FlashCardSetEnabled(
    FlashCardPracticeId Id,
    FlashCardSetId SetId,
    string SetName,
    ImmutableArray<FlashCardTier> Tiers,
    UserId ConfiguredBy,
    DateTimeOffset OccurredAt);

public sealed record FlashCardSetDisabled(FlashCardPracticeId Id, FlashCardSetId SetId, UserId ConfiguredBy, DateTimeOffset OccurredAt);

// One result per first-pass slot. Question and Answer are snapshotted (looked up server-side, never
// taken from the client) so a later card edit or set delete does not rewrite history; a card that
// appears in two slots has two results with the same CardKey. Adaptive repetition aggregates by
// CardKey.
public sealed record FlashCardResult(int Slot, string CardKey, string Question, string Answer, bool KnewFirstTime, int TimesShown);

public sealed record FlashCardGameFinished(
    FlashCardPracticeId Id,
    Guid GameId,
    FlashCardSetId SetId,
    string SetName,
    FlashCardTierId TierId,
    DateOnly FinishedOn,
    int CardCount,
    ImmutableArray<FlashCardResult> Results,
    int Stars,
    DateTimeOffset OccurredAt);
