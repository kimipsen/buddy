using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record ClearMedicineEffectNote(UserId UserId, UserId ChildId, DateOnly Date)
{
    public static ClearMedicineEffectNote FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly date) =>
        new(principal.GetRequiredUserId(), childId, date);
}
