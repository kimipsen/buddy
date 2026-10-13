using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record WithdrawChecklistSuggestion(UserId UserId, TaskTemplateId TemplateId, ChecklistSuggestionId SuggestionId)
{
    public static WithdrawChecklistSuggestion FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, ChecklistSuggestionId suggestionId) =>
        new(principal.GetRequiredUserId(), templateId, suggestionId);
}
