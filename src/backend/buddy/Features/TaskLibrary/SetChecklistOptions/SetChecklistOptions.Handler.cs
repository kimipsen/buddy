using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class SetChecklistOptionsHandler
{
    public static async Task<Result<TaskTemplate>> Handle(
        SetChecklistOptions command,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, TaskTemplate>();
        }

        if (loaded.Template.Kind != TaskTemplateKind.Checklist)
        {
            return new Result<TaskTemplate>.Validation(ValidationProblem.Of("Only a packing list has checklist options."));
        }

        var before = loaded.Template.Checklist;
        var after = new ChecklistOptions(command.EveningBeforeFrom);

        if (before == after)
        {
            return new Result<TaskTemplate>.Success(loaded.Template);
        }

        var changed = new ChecklistOptionsChanged(command.TemplateId, before, after, userId, time.GetUtcNow());

        await templates.AppendAsync(command.TemplateId, [changed], cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Replay([.. loaded.Events, changed]));
    }
}
