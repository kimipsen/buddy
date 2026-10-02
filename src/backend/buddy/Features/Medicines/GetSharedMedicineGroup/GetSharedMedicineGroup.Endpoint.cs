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
        medicines.MapGet("/children/{childId:guid}/group-share", async Task<Results<Ok<SharedMedicineGroupResponse>, NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetSharedMedicineGroup.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<MedicineGroupShare>>(query, cancellationToken);

            return result switch
            {
                Result<MedicineGroupShare>.Success(MedicineGroupShare.Shared(var groupId, var groupName)) => TypedResults.Ok(new SharedMedicineGroupResponse(groupId.Value, groupName)),
                Result<MedicineGroupShare>.Success(MedicineGroupShare.NotShared) => TypedResults.NoContent(),
                Result<MedicineGroupShare>.Forbidden => TypedResults.Forbid(),
                Result<MedicineGroupShare>.NotFound => TypedResults.NotFound(),
                // GetSharedMedicineGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<MedicineGroupShare>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetSharedMedicineGroup");

        return medicines;
    }
}

// 204 No Content when the medicine isn't shared with a group.
public sealed record SharedMedicineGroupResponse(Guid GroupId, string GroupName);
