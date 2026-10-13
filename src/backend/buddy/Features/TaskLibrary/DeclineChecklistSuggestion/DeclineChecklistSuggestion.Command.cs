using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record DeclineChecklistSuggestion(UserId UserId, TaskTemplateId TemplateId, ChecklistSuggestionId SuggestionId)
{
    public static DeclineChecklistSuggestion FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, ChecklistSuggestionId suggestionId) =>
        new(principal.GetRequiredUserId(), templateId, suggestionId);
}
