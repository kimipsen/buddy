using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.PrintTemplates;

public static class DeletePrintTemplateHandler
{
    public static async Task<Result<Unit>> Handle(
        DeletePrintTemplate command,
        IPrintTemplateEventStore store,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        // A deleted template is treated as missing, so deleting twice is a 404 the second time --
        // the same rule DeleteCalendar follows.
        var (template, access) = await PrintTemplateLoader.LoadForManageAsync(store, command.TemplateId, userId, groups, guardians, cancellationToken);

        if (access != PrintTemplateAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var deleted = new PrintTemplateDeleted(template!.Id, userId, DateTimeOffset.UtcNow);

        await store.AppendAsync(template.Id, [deleted], PrintTemplateIndexDocument.From(PrintTemplate.Fold(template, deleted)!), cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
