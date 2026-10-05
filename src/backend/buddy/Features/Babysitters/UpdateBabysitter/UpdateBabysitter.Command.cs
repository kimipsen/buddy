using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public sealed record UpdateBabysitter(UserId UserId, BabysitterId BabysitterId, string Name, string ContactInfo)
{
    public static UpdateBabysitter FromClaims(ClaimsPrincipal principal, BabysitterId babysitterId, string name, string contactInfo) =>
        new(principal.GetRequiredUserId(), babysitterId, name, contactInfo);
}
