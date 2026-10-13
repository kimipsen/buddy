using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record SetChecklistOptions(UserId UserId, TaskTemplateId TemplateId, TimeOnly? EveningBeforeFrom)
{
    public static SetChecklistOptions FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, TimeOnly? eveningBeforeFrom) =>
        new(principal.GetRequiredUserId(), templateId, eveningBeforeFrom);
}
