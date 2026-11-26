using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record DisableFlashCardSet(UserId UserId, UserId ChildId, FlashCardSetId SetId)
{
    public static DisableFlashCardSet FromClaims(ClaimsPrincipal principal, UserId childId, FlashCardSetId setId) =>
        new(principal.GetRequiredUserId(), childId, setId);
}
