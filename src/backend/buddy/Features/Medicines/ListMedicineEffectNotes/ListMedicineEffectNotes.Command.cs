using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record ListMedicineEffectNotes(UserId UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ListMedicineEffectNotes FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetRequiredUserId(), childId, from, to);
}
