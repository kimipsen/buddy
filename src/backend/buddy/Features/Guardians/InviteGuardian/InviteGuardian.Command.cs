using System.Security.Claims;

using buddy.Common.RateLimiting;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record InviteGuardian(UserId UserId, UserId ChildId, string Email, GuardianKind Kind)
{
    public static InviteGuardian FromClaims(ClaimsPrincipal principal, UserId childId, string email, GuardianKind kind) =>
        new(principal.GetRequiredUserId(), childId, email, kind);
}

public sealed record GuardianInviteSummary(Guid Id, string Email, GuardianKind Kind, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt)
{
    public static GuardianInviteSummary FromDocument(GuardianInviteDocument document) =>
        new(document.Id, document.InvitedEmail, document.Kind, document.CreatedAt, document.ExpiresAt);
}

// Distinct from the shared Result<T> only for the resend cooldown (409, see ResendCooldown). The
// handler never produces Forbidden or a validation failure, so neither is a case here.
public union InviteGuardianOutcome(InviteGuardianOutcome.Success, InviteGuardianOutcome.NotFound, ResendCooldownActive)
{
    public sealed record Success(GuardianInviteSummary Invite);
    public sealed record NotFound;
}
