using System.Security.Claims;

using buddy.Common.RateLimiting;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record InviteToGroup(UserId UserId, GroupId GroupId, string Email, GroupRole Role)
{
    public static InviteToGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, string email, GroupRole role) =>
        new(principal.GetRequiredUserId(), groupId, email, role);
}

public sealed record GroupInviteSummary(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt)
{
    public static GroupInviteSummary FromDocument(GroupInviteDocument document) =>
        new(document.Id, document.InvitedEmail, document.Role, document.CreatedAt, document.ExpiresAt);
}

// Distinct from the shared Result<T> only for the resend cooldown (409, see ResendCooldown). The
// handler has no validation failure of its own, so there's no Validation case.
public union InviteToGroupOutcome(InviteToGroupOutcome.Success, InviteToGroupOutcome.NotFound, InviteToGroupOutcome.Forbidden, ResendCooldownActive)
{
    public sealed record Success(GroupInviteSummary Invite);
    public sealed record NotFound;
    public sealed record Forbidden;
}
