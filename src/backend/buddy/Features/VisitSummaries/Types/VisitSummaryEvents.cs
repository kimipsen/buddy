using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public union VisitSummaryEvent(
    VisitSummaryCreated,
    VisitSummaryUpdated,
    VisitSummaryDeleted,
    VisitSummaryShareLinkCreated,
    VisitSummaryShareLinkRevoked
)
{
    public static VisitSummaryEvent FromPayload(object payload) => payload switch
    {
        VisitSummaryCreated e => e,
        VisitSummaryUpdated e => e,
        VisitSummaryDeleted e => e,
        VisitSummaryShareLinkCreated e => e,
        VisitSummaryShareLinkRevoked e => e,
        _ => throw new ArgumentException($"Unknown visit summary event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        VisitSummaryCreated => nameof(VisitSummaryCreated),
        VisitSummaryUpdated => nameof(VisitSummaryUpdated),
        VisitSummaryDeleted => nameof(VisitSummaryDeleted),
        VisitSummaryShareLinkCreated => nameof(VisitSummaryShareLinkCreated),
        VisitSummaryShareLinkRevoked => nameof(VisitSummaryShareLinkRevoked),
    };
}

public sealed record VisitSummaryCreated(
    VisitSummaryId Id,
    UserId ChildId,
    VisitSummaryDetails Details,
    UserId CreatedBy,
    DateTimeOffset OccurredAt);

// Full overwrite of the editable details. Identical content appends nothing.
public sealed record VisitSummaryUpdated(
    VisitSummaryId Id,
    VisitSummaryDetails Before,
    VisitSummaryDetails After,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);

// Soft delete. Every link of the summary stops working with it (Rehydrate revokes them all).
public sealed record VisitSummaryDeleted(VisitSummaryId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);

// TokenHash is the SHA-256 of the plaintext token, never the token itself (ShareSecret).
public sealed record VisitSummaryShareLinkCreated(
    VisitSummaryId Id,
    VisitSummaryShareLinkId LinkId,
    string TokenHash,
    DateTimeOffset? ExpiresAt,
    UserId CreatedBy,
    DateTimeOffset OccurredAt);

public sealed record VisitSummaryShareLinkRevoked(
    VisitSummaryId Id,
    VisitSummaryShareLinkId LinkId,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);
