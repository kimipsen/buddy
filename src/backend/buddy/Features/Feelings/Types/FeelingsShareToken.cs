using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record FeelingsShareTokenId(Guid Value)
{
    public static FeelingsShareTokenId New() => new(Guid.CreateVersion7());
}

// A hashed, revocable, optionally expiring link that lets a doctor read one child's check-ins
// without a Buddy account. A copy of SleepDiaryShareToken with its own id type
// (docs/backend/analysis/feelings-check-in.md, Question 8).
public sealed record FeelingsShareToken(
    FeelingsShareTokenId Id,
    UserId ChildId,
    string TokenHash,
    UserId CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked)
{
    public static FeelingsShareToken? Rehydrate(IEnumerable<FeelingsShareTokenEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static FeelingsShareToken Start(FeelingsShareTokenEvent @event) => @event switch
    {
        FeelingsShareTokenCreated created => new FeelingsShareToken(
            created.Id,
            created.ChildId,
            created.TokenHash,
            created.CreatedBy,
            created.OccurredAt,
            created.ExpiresAt,
            IsRevoked: false),
        _ => throw EventReplay.NotAStartEvent(nameof(FeelingsShareToken), @event.EventType)
    };

    public static FeelingsShareToken Advance(FeelingsShareToken token, FeelingsShareTokenEvent @event) => @event switch
    {
        FeelingsShareTokenRevoked => token with { IsRevoked = true },
        FeelingsShareTokenCreated => throw EventReplay.AlreadyStarted(nameof(FeelingsShareToken), @event.EventType)
    };
}
