using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

// Shared by every Manage-tier write: rehydrate through ReadAsync (so the append that follows is an
// expected-version append), then authorize against the loaded aggregate, like Calendars does.
internal static class PrintTemplateLoader
{
    public static async Task<(PrintTemplate? Template, PrintTemplateAccess Access)> LoadForManageAsync(
        IPrintTemplateEventStore store,
        PrintTemplateId id,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var template = PrintTemplate.Rehydrate(await store.ReadAsync(id, cancellationToken));
        var access = await PrintTemplateAuthorization.CheckManage(template, callerId, groups, guardians, cancellationToken);

        return (access == PrintTemplateAccess.Allowed ? template : null, access);
    }
}
