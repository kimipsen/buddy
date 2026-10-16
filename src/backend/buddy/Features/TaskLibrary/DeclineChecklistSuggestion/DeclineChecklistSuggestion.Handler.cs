using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class DeclineChecklistSuggestionHandler
{
    public static async Task<ChecklistSuggestionOutcome> Handle(
        DeclineChecklistSuggestion command,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, command.UserId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return ChecklistSuggestionOutcome.Denied(resolved);
        }

        return await ChecklistSuggestionResolution.ResolveAsync(
            loaded,
            command.SuggestionId,
            ChecklistSuggestionStatus.Declined,
            now => [new ChecklistItemSuggestionDeclined(command.TemplateId, command.SuggestionId, command.UserId, now)],
            templates,
            time, cancellationToken);
    }
}
