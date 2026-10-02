using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record DeletePrintTemplate(UserId? UserId, PrintTemplateId TemplateId)
{
    public static DeletePrintTemplate FromClaims(ClaimsPrincipal principal, PrintTemplateId templateId) =>
        new(principal.GetUserId(), templateId);
}
