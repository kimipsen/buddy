using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record InviteGuardian(UserId? UserId, UserId ChildId, string Email, GuardianKind Kind)
{
    public static InviteGuardian FromClaims(ClaimsPrincipal principal, UserId childId, string email, GuardianKind kind) =>
        new(principal.GetUserId(), childId, email, kind);
}

public sealed record GuardianInviteSummary(Guid Id, string Email, GuardianKind Kind, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt)
{
    public static GuardianInviteSummary FromDocument(GuardianInviteDocument document) =>
        new(document.Id, document.InvitedEmail, document.Kind, document.CreatedAt, document.ExpiresAt);
}
