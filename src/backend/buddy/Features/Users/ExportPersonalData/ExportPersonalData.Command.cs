using System.Security.Claims;

namespace buddy.Features.Users;

// Everything Buddy holds about the caller (and the children they guard) -- see PersonalDataExport.
public sealed record ExportPersonalData(UserId UserId)
{
    public static ExportPersonalData FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
