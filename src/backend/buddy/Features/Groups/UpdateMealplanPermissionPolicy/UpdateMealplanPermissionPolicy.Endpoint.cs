using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Mealplans;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class UpdateMealplanPermissionPolicyEndpoint
{
    public static RouteGroupBuilder MapUpdateMealplanPermissionPolicy(this RouteGroupBuilder groups)
    {
        groups.MapPut("/{groupId:guid}/mealplan-permission-policy", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            UpdateMealplanPermissionPolicyRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            foreach (var role in Enum.GetValues<GroupRole>())
            {
                if (!request.Policy.ContainsKey(role))
                {
                    return TypedResults.BadRequest(buddy.Common.Validation.ValidationProblem.Of($"The policy must include an entry for every group role; '{role}' is missing.").ToEnvelope(httpContext));
                }
            }

            // Rate is the child's own tier (MealplanAuthorization.CheckRate) and is never a
            // meaningful value here -- only None/Manage describe a group member's access.
            foreach (var (role, tier) in request.Policy)
            {
                if (tier == MealplanAccessTier.Rate)
                {
                    return TypedResults.BadRequest(buddy.Common.Validation.ValidationProblem.Of($"'{tier}' is not a valid meal plan permission for group role '{role}'.").ToEnvelope(httpContext));
                }
            }

            var policy = request.Policy.ToImmutableDictionary();
            var command = UpdateMealplanPermissionPolicy.FromClaims(principal, new GroupId(groupId), policy);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // UpdateMealplanPermissionPolicyHandler never produces Validation, but BadRequest
                // is already part of this route's declared results (used above), so map it there
                // if it ever did.
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("UpdateGroupMealplanPermissionPolicy");

        return groups;
    }
}

public sealed record UpdateMealplanPermissionPolicyRequest(IReadOnlyDictionary<GroupRole, MealplanAccessTier> Policy);
