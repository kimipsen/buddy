using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class ReorderSubtasksHandler
{
    public static async Task<Result<TaskTemplate>> Handle(
        ReorderSubtasks command,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<TaskTemplate>.NotFound();
        }

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, TaskTemplate>();
        }

        var currentIds = loaded.Template.Subtasks.Select(s => s.Id).ToHashSet();

        // State-dependent (needs the loaded TaskTemplate's current subtask ids), so this stays as
        // handler code rather than a FluentValidation rule -- see
        // AssignMealToSlotHandler.AssignForChildAsync's archived-meal check for the same
        // reasoning. NewOrder must be exactly a permutation of the template's current subtasks:
        // same count and same set of ids, or the SubtasksReordered fold invariant TaskTemplate.
        // Rehydrate relies on (every id in After already exists) would be broken.
        if (command.NewOrder.Count != currentIds.Count || !currentIds.SetEquals(command.NewOrder))
        {
            return new Result<TaskTemplate>.Validation(
                ValidationProblem.Of("NewOrder must contain exactly the template's current subtasks, each exactly once."));
        }

        var reordered = new SubtasksReordered(command.TemplateId, command.NewOrder, userId, DateTimeOffset.UtcNow);

        await templates.AppendAsync(command.TemplateId, [reordered], cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Rehydrate([.. loaded.Events, reordered])!);
    }
}
