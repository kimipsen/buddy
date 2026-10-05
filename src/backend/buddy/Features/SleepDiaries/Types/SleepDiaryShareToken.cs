using System.Security.Cryptography;
using System.Text;

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
// IcalToken, a link can expire (docs/backend/analysis/sleep-diary.md, Question 5).
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

// A copy of Calendars' IcalToken, not a reference to it: vertical slices here prefer local
// duplication over a cross-feature dependency for a dozen lines (the open question in
// sleep-diary.md). Only the hash is ever persisted.
public static class SleepDiaryShareSecret
{
    private const int TokenSizeInBytes = 32;

    public static (string Token, string Hash) Generate()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenSizeInBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
