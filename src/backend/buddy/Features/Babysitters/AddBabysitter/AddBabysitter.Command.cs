using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public sealed record AddBabysitter(UserId UserId, string Name, string ContactInfo)
{
    public static AddBabysitter FromClaims(ClaimsPrincipal principal, string name, string contactInfo) =>
        new(principal.GetRequiredUserId(), name, contactInfo);
}
