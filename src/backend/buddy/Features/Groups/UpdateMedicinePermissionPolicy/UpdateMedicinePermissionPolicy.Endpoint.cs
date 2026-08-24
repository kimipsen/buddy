using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;
using buddy.Features.Medicines;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class UpdateMedicinePermissionPolicyEndpoint
{
    public static RouteGroupBuilder MapUpdateMedicinePermissionPolicy(this RouteGroupBuilder groups)
    {
        groups.MapPut("/{groupId:guid}/medicine-permission-policy", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            UpdateMedicinePermissionPolicyRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            foreach (var role in Enum.GetValues<GroupRole>())
            {
                if (!request.Policy.ContainsKey(role))
                {
                    return TypedResults.BadRequest($"The policy must include an entry for every group role; '{role}' is missing.");
                }
            }

            // Mark is the two-principal (child/guardian) tier (MedicineAuthorization.CheckMark)
            // and is never a meaningful value here -- only None/Manage describe a group member's
            // access, the same rule MealplanAccessTier.Rate follows for meal plans.
            foreach (var (role, tier) in request.Policy)
            {
                if (tier == MedicineAccessTier.Mark)
                {
                    return TypedResults.BadRequest($"'{tier}' is not a valid medicine permission for group role '{role}'.");
                }
            }

            var policy = request.Policy.ToImmutableDictionary();
            var command = UpdateMedicinePermissionPolicy.FromClaims(principal, new GroupId(groupId), policy);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // UpdateMedicinePermissionPolicyHandler never produces Validation, but
                // BadRequest is already part of this route's declared results (used above), so
                // map it there if it ever did.
                Result<Unit>.Validation(var message) => TypedResults.BadRequest(message),
            };
        })
        .WithName("UpdateGroupMedicinePermissionPolicy");

        return groups;
    }
}

public sealed record UpdateMedicinePermissionPolicyRequest(IReadOnlyDictionary<GroupRole, MedicineAccessTier> Policy);
