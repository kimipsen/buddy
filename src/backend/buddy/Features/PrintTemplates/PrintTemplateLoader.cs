using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

// Shared by every Manage-tier write: rehydrate through ReadAsync (so the append that follows is an
// expected-version append), then authorize against the loaded aggregate, like Calendars does.
internal static class PrintTemplateLoader
{
    // Success carries the template only when access is Allowed, so callers never see a template
    // they aren't allowed to manage -- or a null one.
    public static async Task<Result<PrintTemplate>> LoadForManageAsync(
        IPrintTemplateEventStore store,
        PrintTemplateId id,
        UserId callerId,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var template = PrintTemplate.Rehydrate(await store.ReadAsync(id, cancellationToken));

        if (template is null)
        {
            return new Result<PrintTemplate>.NotFound();
        }

        var access = await PrintTemplateAuthorization.CheckManage(template, callerId, groups, guardians, cancellationToken);

        return access == PrintTemplateAccess.Allowed
            ? new Result<PrintTemplate>.Success(template)
            : access.ToDeniedResult<PrintTemplate>();
    }
}
