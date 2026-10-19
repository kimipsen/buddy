using System.Security.Claims;

using buddy.Common.RateLimiting;
using buddy.Common.Validation;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record InviteToGroup(UserId UserId, GroupId GroupId, string Email, GroupRole Role)
{
    public static InviteToGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, string email, GroupRole role) =>
        new(principal.GetRequiredUserId(), groupId, email, role);
}

// InviteUrl is the same link the invite email carries, handed back so the inviter can also share
// it themself (SMS, chat). Only this response has it: the stored invite keeps just the token's
// hash, so the link can't be shown again later.
public sealed record GroupInviteSummary(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt, string InviteUrl);

// Distinct from the shared Result<T> only for the resend cooldown (409, see ResendCooldown).
public union InviteToGroupOutcome(InviteToGroupOutcome.Success, InviteToGroupOutcome.NotFound, InviteToGroupOutcome.Forbidden, InviteToGroupOutcome.Validation, ResendCooldownActive)
{
    public sealed record Success(GroupInviteSummary Invite);
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(ValidationProblem Problem);
}
