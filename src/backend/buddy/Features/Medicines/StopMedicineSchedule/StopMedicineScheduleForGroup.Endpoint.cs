using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class StopMedicineScheduleForGroupEndpoint
{
    public static RouteGroupBuilder MapStopMedicineScheduleForGroup(this RouteGroupBuilder medicines)
    {
        medicines.MapDelete("/groups/{groupId:guid}/children/{childId:guid}/schedules/{medicineId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid childId,
            Guid medicineId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = StopMedicineScheduleForGroup.FromClaims(principal, new GroupId(groupId), new UserId(childId), new MedicineId(medicineId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // StopMedicineScheduleForGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("StopMedicineScheduleForGroup");

        return medicines;
    }
}
