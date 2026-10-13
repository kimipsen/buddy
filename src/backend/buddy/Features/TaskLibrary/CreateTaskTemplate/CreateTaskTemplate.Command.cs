using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record CreateTaskTemplate(
    UserId UserId, UserId ChildId, string Name, Icon Icon, Color Color, TaskTemplateKind Kind = TaskTemplateKind.Routine, TimeOnly? EveningBeforeFrom = null)
{
    public static CreateTaskTemplate FromClaims(
        ClaimsPrincipal principal, UserId childId, string name, Icon icon, Color color, TaskTemplateKind kind, TimeOnly? eveningBeforeFrom) =>
        new(principal.GetRequiredUserId(), childId, name, icon, color, kind, eveningBeforeFrom);
}
