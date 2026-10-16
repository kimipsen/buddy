using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public static class UpdateSubtaskHandler
{
    public static async Task<Result<TaskTemplate>> Handle(
        UpdateSubtask command,
        IValidator<UpdateSubtask> validator,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<TaskTemplate>.Validation(problem);
        }

        var userId = command.UserId;

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, TaskTemplate>();
        }

        if (SubtaskDurationRule.Check(loaded.Template.Kind, command.Duration) is { } durationProblem)
        {
            return new Result<TaskTemplate>.Validation(durationProblem);
        }

        var before = loaded.Template.Subtasks.FirstOrDefault(s => s.Id == command.SubtaskId);

        if (before is null)
        {
            return new Result<TaskTemplate>.NotFound();
        }

        var after = new Subtask(command.SubtaskId, command.Title, command.Icon, command.Duration, command.Weekdays);

        if (before == after)
        {
            return new Result<TaskTemplate>.Success(loaded.Template);
        }

        var updated = new SubtaskUpdated(command.TemplateId, command.SubtaskId, before, after, userId, time.GetUtcNow());

        await templates.AppendAsync(command.TemplateId, [updated], cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Replay([.. loaded.Events, updated]));
    }
}
