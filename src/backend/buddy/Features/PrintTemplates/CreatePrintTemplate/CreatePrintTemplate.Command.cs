using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

// GroupId null = a template owned by the caller; set = owned by that group.
public sealed record CreatePrintTemplate(UserId UserId, string Name, GroupId? GroupId)
{
    public static CreatePrintTemplate FromClaims(ClaimsPrincipal principal, string name, GroupId? groupId) =>
        new(principal.GetRequiredUserId(), name, groupId);
}
