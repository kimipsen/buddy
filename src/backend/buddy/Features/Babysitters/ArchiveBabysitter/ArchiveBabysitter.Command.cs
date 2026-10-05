using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public sealed record ArchiveBabysitter(UserId UserId, BabysitterId BabysitterId)
{
    public static ArchiveBabysitter FromClaims(ClaimsPrincipal principal, BabysitterId babysitterId) =>
        new(principal.GetRequiredUserId(), babysitterId);
}
