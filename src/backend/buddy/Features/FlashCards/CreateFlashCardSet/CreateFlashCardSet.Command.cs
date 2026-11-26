using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record CreateFlashCardSet(UserId UserId, string Name, FlashCardSetKind Kind, MathRule? Math)
{
    public static CreateFlashCardSet FromClaims(ClaimsPrincipal principal, string name, FlashCardSetKind kind, MathRule? math) =>
        new(principal.GetRequiredUserId(), name, kind, math);
}
