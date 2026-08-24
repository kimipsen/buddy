using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class GetSharedMedicineGroupEndpoint
{
    public static RouteGroupBuilder MapGetSharedMedicineGroup(this RouteGroupBuilder medicines)
    {
        medicines.MapGet("/children/{childId:guid}/group-share", async Task<Results<Ok<SharedMedicineGroupResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetSharedMedicineGroup.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<GroupId?>>(query, cancellationToken);

            return result switch
            {
                Result<GroupId?>.Success(var groupId) => TypedResults.Ok(new SharedMedicineGroupResponse(groupId?.Value)),
                Result<GroupId?>.Forbidden => TypedResults.Forbid(),
                Result<GroupId?>.NotFound => TypedResults.NotFound(),
                // GetSharedMedicineGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<GroupId?>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetSharedMedicineGroup");

        return medicines;
    }
}

public sealed record SharedMedicineGroupResponse(Guid? GroupId);
