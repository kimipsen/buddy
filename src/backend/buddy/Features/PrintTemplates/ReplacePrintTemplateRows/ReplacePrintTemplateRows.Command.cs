using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record ReplacePrintTemplateRows(UserId UserId, PrintTemplateId TemplateId, IReadOnlyList<PrintTemplateRow> Rows)
{
    public static ReplacePrintTemplateRows FromClaims(ClaimsPrincipal principal, PrintTemplateId templateId, IReadOnlyList<PrintTemplateRow> rows) =>
        new(principal.GetRequiredUserId(), templateId, rows);
}
