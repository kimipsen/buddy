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
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<TaskTemplate>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<TaskTemplate>.NotFound();
        }

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, TaskTemplate>();
        }

        var before = loaded.Template.Subtasks.FirstOrDefault(s => s.Id == command.SubtaskId);

        if (before is null)
        {
            return new Result<TaskTemplate>.NotFound();
        }

        var after = new Subtask(command.SubtaskId, command.Title, command.Icon, command.Duration);

        if (before == after)
        {
            return new Result<TaskTemplate>.Success(loaded.Template);
        }

        var updated = new SubtaskUpdated(command.TemplateId, command.SubtaskId, before, after, userId, DateTimeOffset.UtcNow);

        await templates.AppendAsync(command.TemplateId, [updated], cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Rehydrate([.. loaded.Events, updated])!);
    }
}
