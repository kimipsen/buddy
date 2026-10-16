using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

// One prepared doctor's visit: the period and sections the guardian chose, their note, and the
// links that let a doctor read the report without an account. The report itself is never stored;
// it is read live from the sleep diary, medicines and feelings (Questions 2 and 4).
//
// Unlike the sleep diary and feelings, the share links live on this stream rather than as their own
// aggregates: a link belongs to one summary, and deleting the summary must revoke them all in one
// event.
public sealed record VisitSummary(
    VisitSummaryId Id,
    UserId ChildId,
    VisitSummaryDetails Details,
    ImmutableList<VisitSummaryShareLink> ShareLinks,
    UserId CreatedBy,
    UserId LastModifiedBy,
    DateTimeOffset LastModifiedAt,
    bool IsDeleted)
{
    public static VisitSummary? Rehydrate(IEnumerable<VisitSummaryEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static VisitSummary Replay(IEnumerable<VisitSummaryEvent> events) => EventReplay.Replay(events, Start, Advance);

    public IReadOnlyList<VisitSummaryShareLink> LiveLinks(DateTimeOffset now) => [.. ShareLinks.Where(l => l.IsLive(now))];

    public VisitSummaryShareLink? FindLink(VisitSummaryShareLinkId linkId) => ShareLinks.Find(l => l.Id == linkId);

    public static VisitSummary Start(VisitSummaryEvent @event) => @event switch
    {
        VisitSummaryCreated created => new VisitSummary(
            created.Id,
            created.ChildId,
            created.Details,
            [],
            created.CreatedBy,
            created.CreatedBy,
            created.OccurredAt,
            IsDeleted: false),
        _ => throw EventReplay.NotAStartEvent(nameof(VisitSummary), @event.EventType)
    };

    public static VisitSummary Advance(VisitSummary summary, VisitSummaryEvent @event) => @event switch
    {
        VisitSummaryUpdated updated => summary with
        {
            Details = updated.After,
            LastModifiedBy = updated.ModifiedBy,
            LastModifiedAt = updated.OccurredAt
        },
        VisitSummaryDeleted deleted => summary with
        {
            ShareLinks = [.. summary.ShareLinks.Select(l => l with { IsRevoked = true })],
            LastModifiedBy = deleted.ModifiedBy,
            LastModifiedAt = deleted.OccurredAt,
            IsDeleted = true
        },
        VisitSummaryShareLinkCreated created => summary with
        {
            ShareLinks = summary.ShareLinks.Add(new VisitSummaryShareLink(
                created.LinkId, created.TokenHash, created.CreatedBy, created.OccurredAt, created.ExpiresAt, IsRevoked: false))
        },
        VisitSummaryShareLinkRevoked revoked => summary with
        {
            ShareLinks = [.. summary.ShareLinks.Select(l => l.Id == revoked.LinkId ? l with { IsRevoked = true } : l)]
        },
        VisitSummaryCreated => throw EventReplay.AlreadyStarted(nameof(VisitSummary), @event.EventType)
    };
}

public sealed record VisitSummaryShareLink(
    VisitSummaryShareLinkId Id,
    string TokenHash,
    UserId CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked)
{
    public bool IsLive(DateTimeOffset now) => !IsRevoked && (ExpiresAt is null || ExpiresAt > now);
}
