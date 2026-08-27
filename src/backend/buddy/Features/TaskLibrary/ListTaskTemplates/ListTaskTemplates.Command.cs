using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record ListTaskTemplates(UserId? UserId, UserId ChildId)
{
    public static ListTaskTemplates FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}
