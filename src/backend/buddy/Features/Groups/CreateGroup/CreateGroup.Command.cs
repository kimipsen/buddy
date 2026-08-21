using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record CreateGroup(UserId? UserId, string Name)
{
    public static CreateGroup FromClaims(ClaimsPrincipal principal, string name) =>
        new(principal.GetUserId(), name);
}

public sealed record CreateGroupResult(Group? Group, bool Unauthenticated = false);
