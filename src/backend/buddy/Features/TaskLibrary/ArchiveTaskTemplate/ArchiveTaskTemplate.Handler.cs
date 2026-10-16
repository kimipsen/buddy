using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class ArchiveTaskTemplateHandler
{
    public static async Task<Result<Unit>> Handle(
        ArchiveTaskTemplate command,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, Unit>();
        }

        // Every write past creation is 404 once archived, so a pending suggestion could never be
        // resolved again: decline them in the same append, so the child sees "not added this time"
        // (docs/backend/analysis/packing-checklists.md, Question 8).
        var now = time.GetUtcNow();
        TaskTemplateEvent[] events =
        [
            .. loaded.Template.Suggestions
                .Where(s => s.IsPending)
                .Select(s => (TaskTemplateEvent)new ChecklistItemSuggestionDeclined(command.TemplateId, s.Id, userId, now)),
            new TaskTemplateArchived(command.TemplateId, userId, now),
        ];

        await templates.AppendAsync(command.TemplateId, events, cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
