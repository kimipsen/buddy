using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class CreateMedicineScheduleForGroupEndpoint
{
    public static RouteGroupBuilder MapCreateMedicineScheduleForGroup(this RouteGroupBuilder medicines)
    {
        medicines.MapPost("/groups/{groupId:guid}/children/{childId:guid}/schedules", async Task<Results<Ok<MedicineScheduleResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid childId,
            CreateMedicineScheduleRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateMedicineScheduleForGroup.FromClaims(
                principal,
                new GroupId(groupId),
                new UserId(childId),
                request.Name,
                request.Dosage,
                new Icon(request.Icon),
                new Color(request.Color),
                request.Times,
                request.StartDate,
                request.EndDate);

            var result = await bus.InvokeAsync<Result<MedicineSchedule>>(command, cancellationToken);

            return result switch
            {
                Result<MedicineSchedule>.Success(var schedule) => TypedResults.Ok(MedicineScheduleResponse.FromSchedule(schedule)),
                Result<MedicineSchedule>.Validation(var message) => TypedResults.BadRequest(message),
                Result<MedicineSchedule>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy resolves to no access at all --
                // MedicineGroupAccess only ever grants or withholds Manage, never a lesser tier.
                Result<MedicineSchedule>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("CreateMedicineScheduleForGroup");

        return medicines;
    }
}
