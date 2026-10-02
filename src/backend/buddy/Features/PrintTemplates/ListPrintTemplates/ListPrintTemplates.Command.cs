using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public sealed record ListPrintTemplates(UserId UserId)
{
    public static ListPrintTemplates FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
