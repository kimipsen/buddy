using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class ArchiveTaskTemplateHandler
{
    public static async Task<Result<Unit>> Handle(
        ArchiveTaskTemplate command,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success)
        {
            return resolved.Reraise<ResolvedTaskTemplate, Unit>();
        }

        await templates.AppendAsync(command.TemplateId, [new TaskTemplateArchived(command.TemplateId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
