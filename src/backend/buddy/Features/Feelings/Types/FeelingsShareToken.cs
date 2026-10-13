using System.Security.Cryptography;
using System.Text;

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

// The third copy of these dozen lines, after IcalToken and SleepDiaryShareSecret: vertical slices
// here prefer local duplication over a cross-feature dependency, and promoting all three to Common
// is a separate refactor (feelings-check-in.md, Decisions). Only the hash is ever persisted.
public static class FeelingsShareSecret
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
