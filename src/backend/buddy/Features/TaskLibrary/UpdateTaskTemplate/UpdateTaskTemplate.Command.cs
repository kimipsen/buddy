using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Name and Color are trimmed here, so the validator, the unchanged check and the stored event see
// the same text.
public sealed record UpdateTaskTemplate(UserId UserId, TaskTemplateId TemplateId, string Name, Icon Icon, Color Color)
{
    public static UpdateTaskTemplate FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, string name, Icon icon, Color color) =>
        new(principal.GetRequiredUserId(), templateId, name.Trim(), icon, new Color(color.Value.Trim()));
}
