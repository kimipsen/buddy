using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Title and Weekdays are the guardian's confirmed values (pre-filled from the child's suggestion by
// the client); the server doesn't fall back to the child's. A null Position appends.
public sealed record ApproveChecklistSuggestion(
    UserId UserId, TaskTemplateId TemplateId, ChecklistSuggestionId SuggestionId, string Title, Weekdays Weekdays, int? Position)
{
    public static ApproveChecklistSuggestion FromClaims(
        ClaimsPrincipal principal, TaskTemplateId templateId, ChecklistSuggestionId suggestionId, string? title, Weekdays weekdays, int? position) =>
        new(principal.GetRequiredUserId(), templateId, suggestionId, title?.Trim() ?? string.Empty, weekdays, position);
}
