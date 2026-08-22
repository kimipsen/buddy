using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class StopMedicineScheduleEndpoint
{
    public static RouteGroupBuilder MapStopMedicineSchedule(this RouteGroupBuilder medicines)
    {
        medicines.MapDelete("/children/{childId:guid}/schedules/{medicineId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid medicineId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = StopMedicineSchedule.FromClaims(principal, new UserId(childId), new MedicineId(medicineId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // StopMedicineScheduleHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("StopMedicineSchedule");

        return medicines;
    }
}
