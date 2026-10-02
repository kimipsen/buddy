using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record ReplacePrintTemplateGuardianColors(UserId UserId, PrintTemplateId TemplateId, IReadOnlyList<GuardianColor> Colors)
{
    public static ReplacePrintTemplateGuardianColors FromClaims(ClaimsPrincipal principal, PrintTemplateId templateId, IReadOnlyList<GuardianColor> colors) =>
        new(principal.GetRequiredUserId(), templateId, colors);
}
