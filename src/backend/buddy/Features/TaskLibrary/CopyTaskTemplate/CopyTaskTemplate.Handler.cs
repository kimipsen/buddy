using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public static class CopyTaskTemplateHandler
{
    public static async Task<Result<TaskTemplate>> Handle(
        CopyTaskTemplate command,
        IValidator<CopyTaskTemplate> validator,
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

        // Manage on the source (404 missing/archived/unrelated, 403 for its own child), then on the
        // target child (404 when the caller isn't their guardian).
        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, TaskTemplate>();
        }

        var targetAccess = await TaskLibraryAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (targetAccess != TaskLibraryAccess.Allowed)
        {
            return targetAccess.ToDeniedResult<TaskTemplate>();
        }

        var source = loaded.Template;
        var copyId = TaskTemplateId.New();
        var now = time.GetUtcNow();

        // Built from the existing events: a copy is an ordinary new template. Every item gets a fresh
        // id, so the two lists never share a completion key; suggestions stay with the source child.
        TaskTemplateEvent[] events =
        [
            new TaskTemplateCreated(copyId, command.ChildId, userId, source.Name, source.Icon, source.Color, now, source.Kind),
            .. source.Checklist == ChecklistOptions.None
                ? []
                : new TaskTemplateEvent[] { new ChecklistOptionsChanged(copyId, ChecklistOptions.None, source.Checklist, userId, now) },
            .. source.Subtasks.Select((subtask, position) =>
                (TaskTemplateEvent)new SubtaskAdded(copyId, subtask with { Id = SubtaskId.New() }, position, userId, now)),
        ];

        var created = await templates.CreateAsync(copyId, events, cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Replay(created));
    }
}
