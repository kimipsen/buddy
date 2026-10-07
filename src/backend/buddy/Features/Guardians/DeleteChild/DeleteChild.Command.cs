using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

// Erases a child's account and data (docs/backend/analysis/gdpr-data-protection.md, Question 3).
// Only the child's sole active guardian may: with co-guardians, each gives up their link first, so no
// parent erases what another relies on.
public sealed record DeleteChild(UserId GuardianId, UserId ChildId)
{
    public const string HasOtherGuardiansCode = "child_has_other_guardians";

    public static DeleteChild FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}

public union DeleteChildOutcome(DeleteChildOutcome.Success, DeleteChildOutcome.NotFound, DeleteChildOutcome.HasOtherGuardians)
{
    public sealed record Success;
    public sealed record NotFound;
    public sealed record HasOtherGuardians;
}
