using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// The owning child proposes an item for their own packing list. Title is trimmed here, so the
// validator, the duplicate checks and the stored event all see the same text.
public sealed record SuggestChecklistItem(UserId UserId, TaskTemplateId TemplateId, string Title, Weekdays Weekdays)
{
    public static SuggestChecklistItem FromClaims(ClaimsPrincipal principal, TaskTemplateId templateId, string? title, Weekdays weekdays) =>
        new(principal.GetRequiredUserId(), templateId, title?.Trim() ?? string.Empty, weekdays);
}
