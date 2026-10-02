using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record GetPrintTemplate(UserId? UserId, PrintTemplateId TemplateId)
{
    public static GetPrintTemplate FromClaims(ClaimsPrincipal principal, PrintTemplateId templateId) =>
        new(principal.GetUserId(), templateId);
}
