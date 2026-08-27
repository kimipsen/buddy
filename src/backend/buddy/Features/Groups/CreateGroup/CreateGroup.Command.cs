using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record CreateGroup(UserId? UserId, string Name)
{
    public static CreateGroup FromClaims(ClaimsPrincipal principal, string name) =>
        new(principal.GetUserId(), name);
}

// Distinct from the shared Result<T>: same reasoning as CreateCalendarOutcome -- there's no
// existing resource here to hide behind an ambiguous 404, so an unauthenticated caller gets a 401.
public union CreateGroupOutcome(CreateGroupOutcome.Success, CreateGroupOutcome.Unauthenticated)
{
    public sealed record Success(GroupWithMemberDetails Group);
    public sealed record Unauthenticated;
}
