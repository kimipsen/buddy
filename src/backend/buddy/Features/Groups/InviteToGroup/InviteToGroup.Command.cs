using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record InviteToGroup(UserId? UserId, GroupId GroupId, string Email, GroupRole Role)
{
    public static InviteToGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, string email, GroupRole role) =>
        new(principal.GetUserId(), groupId, email, role);
}

public sealed record GroupInviteSummary(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt)
{
    public static GroupInviteSummary FromDocument(GroupInviteDocument document) =>
        new(document.Id, document.InvitedEmail, document.Role, document.CreatedAt, document.ExpiresAt);
}
