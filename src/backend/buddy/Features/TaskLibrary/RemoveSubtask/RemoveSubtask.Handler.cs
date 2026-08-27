using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class RemoveSubtaskHandler
{
    public static async Task<Result<Unit>> Handle(
        RemoveSubtask command,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, Unit>();
        }

        if (!loaded.Template.Subtasks.Any(s => s.Id == command.SubtaskId))
        {
            return new Result<Unit>.NotFound();
        }

        await templates.AppendAsync(
            command.TemplateId,
            [new SubtaskRemoved(command.TemplateId, command.SubtaskId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
