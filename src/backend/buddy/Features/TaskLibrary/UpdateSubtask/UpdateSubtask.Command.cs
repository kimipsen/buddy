using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record UpdateSubtask(UserId? UserId, TaskTemplateId TemplateId, SubtaskId SubtaskId, string Title, Icon? Icon, TimeSpan Duration)
{
    public static UpdateSubtask FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, SubtaskId subtaskId, string title, Icon? icon, TimeSpan duration) =>
        new(principal.GetUserId(), templateId, subtaskId, title, icon, duration);
}
