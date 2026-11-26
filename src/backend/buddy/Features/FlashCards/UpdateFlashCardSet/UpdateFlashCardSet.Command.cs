using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

// A null Math keeps the set's current rule (a rename).
public sealed record UpdateFlashCardSet(UserId UserId, FlashCardSetId SetId, string Name, MathRule? Math)
{
    public static UpdateFlashCardSet FromClaims(ClaimsPrincipal principal, FlashCardSetId setId, string name, MathRule? math) =>
        new(principal.GetRequiredUserId(), setId, name, math);
}
