using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public static class ApproveChecklistSuggestionHandler
{
    public static async Task<ChecklistSuggestionOutcome> Handle(
        ApproveChecklistSuggestion command,
        IValidator<ApproveChecklistSuggestion> validator,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, command.UserId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return ChecklistSuggestionOutcome.Denied(resolved);
        }

        // A repeated approval is an idempotent 200 that ignores its body (a retry may not resend
        // it), so the body is only validated for an approval that will happen.
        if (loaded.Template.FindSuggestion(command.SuggestionId) is { Status: ChecklistSuggestionStatus.Approved })
        {
            return new ChecklistSuggestionOutcome.Success(loaded.Template);
        }

        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new ChecklistSuggestionOutcome.Validation(problem);
        }

        var position = command.Position ?? loaded.Template.Subtasks.Count;

        // The item and the approval are one append on one stream, so two guardians approving at
        // once, or an approval racing an archive, end in the existing 409 concurrency_conflict.
        // The icon is left blank, so the item inherits the template's, like AddSubtask.
        return await ChecklistSuggestionResolution.ResolveAsync(
            loaded,
            command.SuggestionId,
            ChecklistSuggestionStatus.Approved,
            now =>
            {
                var subtask = new Subtask(SubtaskId.New(), command.Title, Icon: null, TimeSpan.Zero, command.Weekdays);

                return
                [
                    new SubtaskAdded(command.TemplateId, subtask, position, command.UserId, now),
                    new ChecklistItemSuggestionApproved(command.TemplateId, command.SuggestionId, subtask.Id, command.UserId, now),
                ];
            },
            templates,
            cancellationToken);
    }
}
