using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.FirstThen;

// At most one current board per child plus the recently used cards, which come free from folding
// FirstThenBoardSet. See docs/backend/analysis/first-then-board.md, Questions 2-4.
public sealed record FirstThenBoard(
    FirstThenBoardId Id,
    UserId ChildId,
    FirstThenState State,
    // +1 on every FirstThenBoardSet; 0 before the first.
    int Revision,
    FirstThenCard First,
    FirstThenCard Then,
    int? ThenMinutes,
    // When the First was ticked; drives the Then countdown.
    DateTimeOffset? UnlockedAt,
    // Newest first, distinct by case-insensitive title, at most MaxRecentCards.
    ImmutableList<FirstThenCard> RecentFirsts,
    ImmutableList<FirstThenCard> RecentThens,
    UserId LastModifiedBy,
    DateTimeOffset LastModifiedAt)
{
    public const int MaxRecentCards = 8;

    public static FirstThenBoard? Rehydrate(IEnumerable<FirstThenEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static FirstThenBoard Replay(IEnumerable<FirstThenEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create/Evolve -- JasperFx's projection source generator scans those names on
    // any projection document type (docs/backend/analysis/event-stream-snapshots.md, Question 4/5).
    public static FirstThenBoard Start(FirstThenEvent @event) => @event switch
    {
        FirstThenBoardStarted started => new FirstThenBoard(
            started.Id,
            started.ChildId,
            FirstThenState.None,
            0,
            FirstThenCard.Empty,
            FirstThenCard.Empty,
            null,
            null,
            [],
            [],
            started.ChildId,
            started.OccurredAt),
        _ => throw EventReplay.NotAStartEvent(nameof(FirstThenBoard), @event.EventType)
    };

    public static FirstThenBoard Advance(FirstThenBoard board, FirstThenEvent @event) => @event switch
    {
        FirstThenBoardSet set => board with
        {
            State = FirstThenState.Waiting,
            Revision = set.Revision,
            First = set.First,
            Then = set.Then,
            ThenMinutes = set.ThenMinutes,
            UnlockedAt = null,
            RecentFirsts = PushRecent(board.RecentFirsts, set.First),
            RecentThens = PushRecent(board.RecentThens, set.Then),
            LastModifiedBy = set.SetBy,
            LastModifiedAt = set.OccurredAt
        },
        FirstThenFirstCompleted completed => board with
        {
            State = FirstThenState.Unlocked,
            UnlockedAt = completed.OccurredAt,
            LastModifiedBy = completed.CompletedBy,
            LastModifiedAt = completed.OccurredAt
        },
        FirstThenFirstReopened reopened => board with
        {
            State = FirstThenState.Waiting,
            UnlockedAt = null,
            LastModifiedBy = reopened.ReopenedBy,
            LastModifiedAt = reopened.OccurredAt
        },
        // Keeps Revision and the recent lists: the next board continues from them.
        FirstThenBoardEnded ended => board with
        {
            State = FirstThenState.None,
            First = FirstThenCard.Empty,
            Then = FirstThenCard.Empty,
            ThenMinutes = null,
            UnlockedAt = null,
            LastModifiedBy = ended.EndedBy,
            LastModifiedAt = ended.OccurredAt
        },
        FirstThenBoardStarted => throw EventReplay.AlreadyStarted(nameof(FirstThenBoard), @event.EventType)
    };

    // The card goes to the front; an older card with the same title is dropped, then the list is
    // trimmed.
    private static ImmutableList<FirstThenCard> PushRecent(ImmutableList<FirstThenCard> recent, FirstThenCard card) =>
        [.. recent.RemoveAll(c => c.HasSameTitleAs(card)).Insert(0, card).Take(MaxRecentCards)];
}
