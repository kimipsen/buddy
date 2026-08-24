using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class ListMedicineSchedulesForGroupEndpoint
{
    public static RouteGroupBuilder MapListMedicineSchedulesForGroup(this RouteGroupBuilder medicines)
    {
        medicines.MapGet("/groups/{groupId:guid}/children/{childId:guid}/schedules", async Task<Results<Ok<IReadOnlyCollection<MedicineScheduleResponse>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListMedicineSchedulesForGroup.FromClaims(principal, new GroupId(groupId), new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<MedicineSchedule>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<MedicineSchedule>>.Success(var schedules) =>
                    TypedResults.Ok<IReadOnlyCollection<MedicineScheduleResponse>>([.. schedules.Select(MedicineScheduleResponse.FromSchedule)]),
                Result<IReadOnlyCollection<MedicineSchedule>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<MedicineSchedule>>.NotFound => TypedResults.NotFound(),
                // ListMedicineSchedulesForGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<IReadOnlyCollection<MedicineSchedule>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMedicineSchedulesForGroup");

        return medicines;
    }
}
