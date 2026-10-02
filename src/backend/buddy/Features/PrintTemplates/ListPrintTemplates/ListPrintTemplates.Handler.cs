using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.PrintTemplates;

public static class ListPrintTemplatesHandler
{
    public static async Task<Result<IReadOnlyCollection<PrintTemplateSummary>>> Handle(
        ListPrintTemplates query,
        IPrintTemplateEventStore store,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        // A child has no tier at all, so it sees no templates -- not even its group's.
        if (await ChildVisibility.IsChildAsync(userId, guardians, cancellationToken))
        {
            return new Result<IReadOnlyCollection<PrintTemplateSummary>>.Success([]);
        }

        var groupIds = (await groups.ListForUserAsync(userId, cancellationToken))
            .Select(membership => new GroupId(membership.GroupId))
            .ToList();

        var templates = await store.ListAsync(userId, groupIds, cancellationToken);

        return new Result<IReadOnlyCollection<PrintTemplateSummary>>.Success([.. templates.Select(PrintTemplateSummary.From)]);
    }
}
