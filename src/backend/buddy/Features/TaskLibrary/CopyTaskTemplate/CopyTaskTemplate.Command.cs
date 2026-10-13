using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Copies a template into another child's library (docs/backend/analysis/packing-checklists.md,
// Question 9). ChildId is the target child.
public sealed record CopyTaskTemplate(UserId UserId, TaskTemplateId TemplateId, UserId ChildId)
{
    public static CopyTaskTemplate FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, UserId childId) =>
        new(principal.GetRequiredUserId(), templateId, childId);
}
