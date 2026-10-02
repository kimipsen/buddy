using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.PrintTemplates;

public static class GetPrintTemplateHandler
{
    public static async Task<Result<PrintTemplateResponse>> Handle(
        GetPrintTemplate query,
        IPrintTemplateEventStore store,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<PrintTemplateResponse>.NotFound();
        }

        var template = await store.FindSnapshotAsync(query.TemplateId, cancellationToken);
        var access = await PrintTemplateAuthorization.CheckManage(template, userId, groups, guardians, cancellationToken);

        return access == PrintTemplateAccess.Allowed
            ? new Result<PrintTemplateResponse>.Success(PrintTemplateResponse.From(template!))
            : access.ToDeniedResult<PrintTemplateResponse>();
    }
}
