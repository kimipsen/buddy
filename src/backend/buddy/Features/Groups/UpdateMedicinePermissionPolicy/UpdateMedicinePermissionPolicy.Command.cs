using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Medicines;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record UpdateMedicinePermissionPolicy(UserId? UserId, GroupId GroupId, ImmutableDictionary<GroupRole, MedicineAccessTier> Policy)
{
    public static UpdateMedicinePermissionPolicy FromClaims(ClaimsPrincipal principal, GroupId groupId, ImmutableDictionary<GroupRole, MedicineAccessTier> policy) =>
        new(principal.GetUserId(), groupId, policy);
}
