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
            var result = await bus.InvokeAsync<Result<SharedMedicineGroup?>>(query, cancellationToken);

            return result switch
            {
                Result<SharedMedicineGroup?>.Success(var group) => TypedResults.Ok(new SharedMedicineGroupResponse(group?.Id.Value, group?.Name)),
                Result<SharedMedicineGroup?>.Forbidden => TypedResults.Forbid(),
                Result<SharedMedicineGroup?>.NotFound => TypedResults.NotFound(),
                // GetSharedMedicineGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<SharedMedicineGroup?>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetSharedMedicineGroup");

        return medicines;
    }
}

public sealed record SharedMedicineGroupResponse(Guid? GroupId, string? GroupName);
