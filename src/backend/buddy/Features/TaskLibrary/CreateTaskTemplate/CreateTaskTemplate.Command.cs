using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Name and Color are trimmed here, so the validator and the stored event see the same text.
public sealed record CreateTaskTemplate(
    UserId UserId, UserId ChildId, string Name, Icon Icon, Color Color, TaskTemplateKind Kind = TaskTemplateKind.Routine, TimeOnly? EveningBeforeFrom = null)
{
    public static CreateTaskTemplate FromClaims(
        ClaimsPrincipal principal, UserId childId, string name, Icon icon, Color color, TaskTemplateKind kind, TimeOnly? eveningBeforeFrom) =>
        new(principal.GetRequiredUserId(), childId, name.Trim(), icon, new Color(color.Value.Trim()), kind, eveningBeforeFrom);
}
