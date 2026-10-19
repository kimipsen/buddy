using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Title is trimmed here (as SuggestChecklistItem), so the validator and the stored event see the
// same text.
public sealed record AddSubtask(UserId UserId, TaskTemplateId TemplateId, string Title, Icon? Icon, TimeSpan Duration, int? Position, Weekdays Weekdays = Weekdays.None)
{
    public static AddSubtask FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, string title, Icon? icon, TimeSpan duration, int? position, Weekdays weekdays) =>
        new(principal.GetRequiredUserId(), templateId, title.Trim(), icon, duration, position, weekdays);
}
