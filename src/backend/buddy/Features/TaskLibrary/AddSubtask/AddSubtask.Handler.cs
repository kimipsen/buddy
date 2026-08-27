using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public static class AddSubtaskHandler
{
    public static async Task<Result<TaskTemplate>> Handle(
        AddSubtask command,
        IValidator<AddSubtask> validator,
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

        // Null Position means "append at the end"; Rehydrate's SubtaskAdded fold clamps to
        // [0, Subtasks.Count] regardless, so an explicit out-of-range Position is simply clamped
        // rather than rejected.
        var position = command.Position ?? loaded.Template.Subtasks.Count;
        var subtask = new Subtask(SubtaskId.New(), command.Title, command.Icon, command.Duration);
        var added = new SubtaskAdded(command.TemplateId, subtask, position, userId, DateTimeOffset.UtcNow);

        await templates.AppendAsync(command.TemplateId, [added], cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Rehydrate([.. loaded.Events, added])!);
    }
}
