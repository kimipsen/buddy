using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public enum ChecklistSuggestionStatus
{
    Pending,
    Approved,
    Declined,
    Withdrawn,
}

// An item a child proposed for their own packing list (docs/backend/analysis/packing-checklists.md,
// Question 8). Weekdays is the child's proposal; the approved item takes the guardian's confirmed
// title and days, and SubtaskId points at it. ResolvedBy is the guardian, or the child for a
// withdrawal.
public sealed record ChecklistSuggestion(
    ChecklistSuggestionId Id,
    string Title,
    Weekdays Weekdays,
    UserId SuggestedBy,
    ChecklistSuggestionStatus Status,
    DateTimeOffset SuggestedAt,
    DateTimeOffset? ResolvedAt,
    UserId? ResolvedBy,
    SubtaskId? SubtaskId)
{
    public bool IsPending => Status == ChecklistSuggestionStatus.Pending;
}
