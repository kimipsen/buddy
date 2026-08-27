using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record ReorderSubtasks(UserId? UserId, TaskTemplateId TemplateId, ImmutableList<SubtaskId> NewOrder)
{
    public static ReorderSubtasks FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, ImmutableList<SubtaskId> newOrder) =>
        new(principal.GetUserId(), templateId, newOrder);
}
