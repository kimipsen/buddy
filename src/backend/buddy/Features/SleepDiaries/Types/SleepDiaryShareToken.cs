using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record SleepDiaryShareTokenId(Guid Value)
{
    public static SleepDiaryShareTokenId New() => new(Guid.CreateVersion7());
}

// A hashed, revocable link that lets someone outside the app (a clinician) read one child's diary
// without a Buddy account. Its own small aggregate rather than part of SleepDiary: a diary has zero
// or more live links, and a link's lifecycle has nothing to do with any day's entry. Unlike
// the iCal feeds, a link can expire (docs/backend/analysis/sleep-diary.md, Question 5).
public sealed record SleepDiaryShareToken(
    SleepDiaryShareTokenId Id,
    UserId ChildId,
    string TokenHash,
    UserId CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked)
{
    public static SleepDiaryShareToken? Rehydrate(IEnumerable<SleepDiaryShareTokenEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static SleepDiaryShareToken Start(SleepDiaryShareTokenEvent @event) => @event switch
    {
        SleepDiaryShareTokenCreated created => new SleepDiaryShareToken(
            created.Id,
            created.ChildId,
            created.TokenHash,
            created.CreatedBy,
            created.OccurredAt,
            created.ExpiresAt,
            IsRevoked: false),
        _ => throw EventReplay.NotAStartEvent(nameof(SleepDiaryShareToken), @event.EventType)
    };

    public static SleepDiaryShareToken Advance(SleepDiaryShareToken token, SleepDiaryShareTokenEvent @event) => @event switch
    {
        SleepDiaryShareTokenRevoked => token with { IsRevoked = true },
        SleepDiaryShareTokenCreated => throw EventReplay.AlreadyStarted(nameof(SleepDiaryShareToken), @event.EventType)
    };
}
