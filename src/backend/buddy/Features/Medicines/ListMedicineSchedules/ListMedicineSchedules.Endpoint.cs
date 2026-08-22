using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class ListMedicineSchedulesEndpoint
{
    public static RouteGroupBuilder MapListMedicineSchedules(this RouteGroupBuilder medicines)
    {
        medicines.MapGet("/children/{childId:guid}/schedules", async Task<Results<Ok<IReadOnlyCollection<MedicineScheduleResponse>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<MedicineSchedule>>>(ListMedicineSchedules.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<MedicineSchedule>>.Success(var schedules) =>
                    TypedResults.Ok<IReadOnlyCollection<MedicineScheduleResponse>>([.. schedules.Select(MedicineScheduleResponse.FromSchedule)]),
                Result<IReadOnlyCollection<MedicineSchedule>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<MedicineSchedule>>.NotFound => TypedResults.NotFound(),
                // ListMedicineSchedulesHandler never produces Validation -- there's no BadRequest
                // in this route's declared results, so this collapses to NotFound like the others.
                Result<IReadOnlyCollection<MedicineSchedule>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMedicineSchedules");

        return medicines;
    }
}
