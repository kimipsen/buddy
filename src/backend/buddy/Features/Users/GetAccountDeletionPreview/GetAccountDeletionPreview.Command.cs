using System.Security.Claims;

namespace buddy.Features.Users;

// What DELETE /users/me would also take with it -- see AccountDeletionPreview.
public sealed record GetAccountDeletionPreview(UserId UserId)
{
    public static GetAccountDeletionPreview FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
