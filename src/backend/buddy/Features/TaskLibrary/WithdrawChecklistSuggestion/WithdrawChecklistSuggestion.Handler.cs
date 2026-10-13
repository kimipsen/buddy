using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class WithdrawChecklistSuggestionHandler
{
    public static async Task<ChecklistSuggestionOutcome> Handle(
        WithdrawChecklistSuggestion command,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var resolved = await TaskTemplateAccess.ResolveForOwnerAsync(command.TemplateId, command.UserId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return ChecklistSuggestionOutcome.Denied(resolved);
        }

        return await ChecklistSuggestionResolution.ResolveAsync(
            loaded,
            command.SuggestionId,
            ChecklistSuggestionStatus.Withdrawn,
            now => [new ChecklistItemSuggestionWithdrawn(command.TemplateId, command.SuggestionId, command.UserId, now)],
            templates,
            cancellationToken);
    }
}
