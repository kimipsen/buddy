using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record RenamePrintTemplate(UserId UserId, PrintTemplateId TemplateId, string Name)
{
    public static RenamePrintTemplate FromClaims(ClaimsPrincipal principal, PrintTemplateId templateId, string name) =>
        new(principal.GetRequiredUserId(), templateId, name);
}
