using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public static class SuggestChecklistItemHandler
{
    public static async Task<ChecklistSuggestionOutcome> Handle(
        SuggestChecklistItem command,
        IValidator<SuggestChecklistItem> validator,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new ChecklistSuggestionOutcome.Validation(problem);
        }

        var resolved = await TaskTemplateAccess.ResolveForOwnerAsync(command.TemplateId, command.UserId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return ChecklistSuggestionOutcome.Denied(resolved);
        }

        var template = loaded.Template;

        if (template.Kind != TaskTemplateKind.Checklist)
        {
            return new ChecklistSuggestionOutcome.Validation(ValidationProblem.Of("Only packing lists take suggestions."));
        }

        // A double tap, or asking again while the first one still waits: the first suggestion (and
        // its weekdays) stands.
        if (template.Suggestions.Any(s => s.IsPending && ChecklistSuggestionRules.SameTitle(s.Title, command.Title)))
        {
            return new ChecklistSuggestionOutcome.Success(template);
        }

        if (template.Subtasks.Any(s => ChecklistSuggestionRules.SameTitle(s.Title, command.Title)))
        {
            return new ChecklistSuggestionOutcome.ItemExists();
        }

        if (template.Suggestions.Count(s => s.IsPending) >= ChecklistSuggestionRules.MaxPending)
        {
            return new ChecklistSuggestionOutcome.TooManyPending();
        }

        var suggested = new ChecklistItemSuggested(
            command.TemplateId, ChecklistSuggestionId.New(), command.Title, command.Weekdays, command.UserId, DateTimeOffset.UtcNow);

        await templates.AppendAsync(command.TemplateId, [suggested], cancellationToken);

        return new ChecklistSuggestionOutcome.Success(TaskTemplate.Replay([.. loaded.Events, suggested]));
    }
}
