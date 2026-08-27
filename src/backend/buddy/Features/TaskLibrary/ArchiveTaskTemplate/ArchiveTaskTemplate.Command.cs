using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record ArchiveTaskTemplate(UserId? UserId, TaskTemplateId TemplateId)
{
    public static ArchiveTaskTemplate FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId) =>
        new(principal.GetUserId(), templateId);
}
