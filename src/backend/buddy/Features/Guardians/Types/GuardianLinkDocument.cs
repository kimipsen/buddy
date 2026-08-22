namespace buddy.Features.Guardians;

// Queryable read-model index kept alongside the GuardianLink event stream, the same pattern as
// GroupMembershipDocument/CalendarMembershipDocument. Unlike a group-owned calendar's Calendar.Owner
// (which already carries the GroupId to fetch by), a user-owned calendar only carries the child's
// UserId -- there is no GuardianLinkId in scope at authorization time. This document is the only
// way to answer "does this caller have an active link to this child" without an unindexed scan, so
// its composite Id (BuildId(childId, guardianId)) is the index, same convention as every other
// membership document in this codebase (no explicit Marten index config exists anywhere).
//
// IsRevoked is carried here directly rather than requiring a full GuardianLink rehydration to
// answer the access question, because GuardianKind never gates access (see GuardianKind) -- the
// single boolean is the whole decision, so nothing is lost versus rehydrating the aggregate.
// GuardianLinkId is kept too, so a later append (revoke/kind-change) knows which stream to target.
public sealed record GuardianLinkDocument(string Id, Guid GuardianLinkId, Guid ChildId, Guid GuardianId, GuardianKind Kind, bool IsRevoked)
{
    public static string BuildId(Guid childId, Guid guardianId) => $"{childId}:{guardianId}";
}
