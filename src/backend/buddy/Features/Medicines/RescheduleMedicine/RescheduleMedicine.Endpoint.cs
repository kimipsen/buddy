using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class RescheduleMedicineEndpoint
{
    public static RouteGroupBuilder MapRescheduleMedicine(this RouteGroupBuilder medicines)
    {
        medicines.MapPatch("/children/{childId:guid}/schedules/{medicineId:guid}/schedule", async Task<Results<Ok<MedicineScheduleResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid medicineId,
            RescheduleMedicineRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RescheduleMedicine.FromClaims(
                principal,
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
        .WithName("RescheduleMedicine");

        return medicines;
    }
}

public sealed record RescheduleMedicineRequest(IReadOnlyList<TimeOnly> Times, DateOnly StartDate, DateOnly? EndDate);
