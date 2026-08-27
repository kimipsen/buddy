using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class UpdateMedicineDetailsEndpoint
{
    public static RouteGroupBuilder MapUpdateMedicineDetails(this RouteGroupBuilder medicines)
    {
        medicines.MapPatch("/children/{childId:guid}/schedules/{medicineId:guid}/details", async Task<Results<Ok<MedicineScheduleResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid medicineId,
            UpdateMedicineDetailsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateMedicineDetails.FromClaims(
                principal,
                new UserId(childId),
                new MedicineId(medicineId),
                request.Name,
                request.Dosage,
                new Icon(request.Icon),
                new Color(request.Color));

            var result = await bus.InvokeAsync<Result<MedicineSchedule>>(command, cancellationToken);

            return result switch
            {
                Result<MedicineSchedule>.Success(var schedule) => TypedResults.Ok(MedicineScheduleResponse.FromSchedule(schedule)),
                Result<MedicineSchedule>.Forbidden => TypedResults.Forbid(),
                Result<MedicineSchedule>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MedicineSchedule>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateMedicineDetails");

        return medicines;
    }
}

public sealed record UpdateMedicineDetailsRequest(string Name, string Dosage, string Icon, string Color);
