using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record UpdateSubtask(UserId UserId, TaskTemplateId TemplateId, SubtaskId SubtaskId, string Title, Icon? Icon, TimeSpan Duration, Weekdays Weekdays = Weekdays.None)
{
    public static UpdateSubtask FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, SubtaskId subtaskId, string title, Icon? icon, TimeSpan duration, Weekdays weekdays) =>
        new(principal.GetRequiredUserId(), templateId, subtaskId, title, icon, duration, weekdays);
}
