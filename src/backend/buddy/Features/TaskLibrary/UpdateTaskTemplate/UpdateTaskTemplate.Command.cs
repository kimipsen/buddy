using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record UpdateTaskTemplate(UserId? UserId, TaskTemplateId TemplateId, string Name, Icon Icon, Color Color)
{
    public static UpdateTaskTemplate FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, string name, Icon icon, Color color) =>
        new(principal.GetUserId(), templateId, name, icon, color);
}
