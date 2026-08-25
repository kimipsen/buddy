using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

public sealed record ClearPickup(UserId? UserId, UserId ChildId, DateOnly Date, PickupSlot Slot)
{
    public static ClearPickup FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly date, PickupSlot slot) =>
        new(principal.GetUserId(), childId, date, slot);
}
