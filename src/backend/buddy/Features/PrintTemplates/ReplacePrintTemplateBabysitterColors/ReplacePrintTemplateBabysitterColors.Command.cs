using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record ReplacePrintTemplateBabysitterColors(UserId UserId, PrintTemplateId TemplateId, IReadOnlyList<BabysitterColor> Colors)
{
    public static ReplacePrintTemplateBabysitterColors FromClaims(ClaimsPrincipal principal, PrintTemplateId templateId, IReadOnlyList<BabysitterColor> colors) =>
        new(principal.GetRequiredUserId(), templateId, colors);
}
