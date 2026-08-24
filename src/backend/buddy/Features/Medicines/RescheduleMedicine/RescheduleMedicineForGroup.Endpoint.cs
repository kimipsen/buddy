using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class RescheduleMedicineForGroupEndpoint
{
    public static RouteGroupBuilder MapRescheduleMedicineForGroup(this RouteGroupBuilder medicines)
    {
        medicines.MapPatch("/groups/{groupId:guid}/children/{childId:guid}/schedules/{medicineId:guid}/schedule", async Task<Results<Ok<MedicineScheduleResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid childId,
            Guid medicineId,
            RescheduleMedicineRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RescheduleMedicineForGroup.FromClaims(
                principal,
                new GroupId(groupId),
                new UserId(childId),
                new MedicineId(medicineId),
                request.Times,
                request.StartDate,
                request.EndDate);

            var result = await bus.InvokeAsync<Result<MedicineSchedule>>(command, cancellationToken);

            return result switch
            {
                Result<MedicineSchedule>.Success(var schedule) => TypedResults.Ok(MedicineScheduleResponse.FromSchedule(schedule)),
                Result<MedicineSchedule>.Forbidden => TypedResults.Forbid(),
                Result<MedicineSchedule>.Validation(var message) => TypedResults.BadRequest(message),
                Result<MedicineSchedule>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("RescheduleMedicineForGroup");

        return medicines;
    }
}
