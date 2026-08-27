using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record RemoveSubtask(UserId? UserId, TaskTemplateId TemplateId, SubtaskId SubtaskId)
{
    public static RemoveSubtask FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, SubtaskId subtaskId) =>
        new(principal.GetUserId(), templateId, subtaskId);
}
