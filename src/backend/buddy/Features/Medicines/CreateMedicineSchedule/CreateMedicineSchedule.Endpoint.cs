using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class CreateMedicineScheduleEndpoint
{
    public static RouteGroupBuilder MapCreateMedicineSchedule(this RouteGroupBuilder medicines)
    {
        medicines.MapPost("/children/{childId:guid}/schedules", async Task<Results<Ok<MedicineScheduleResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid childId,
            CreateMedicineScheduleRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateMedicineSchedule.FromClaims(
                principal,
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
                Result<MedicineSchedule>.Forbidden => TypedResults.Forbid(),
                Result<MedicineSchedule>.Validation(var message) => TypedResults.BadRequest(message),
                Result<MedicineSchedule>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("CreateMedicineSchedule");

        return medicines;
    }
}

public sealed record CreateMedicineScheduleRequest(
    string Name,
    string Dosage,
    string Icon,
    string Color,
    IReadOnlyList<TimeOnly> Times,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record MedicineScheduleResponse(
    MedicineId Id,
    Guid ChildId,
    string Name,
    string Dosage,
    string Icon,
    string Color,
    IReadOnlyList<TimeOnly> Times,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsStopped,
    Guid CreatedBy,
    Guid LastModifiedBy)
{
    public static MedicineScheduleResponse FromSchedule(MedicineSchedule schedule) => new(
        schedule.Id,
        schedule.ChildId.Value,
        schedule.Name,
        schedule.Dosage,
        schedule.Icon.Value,
        schedule.Color.Value,
        schedule.Times,
        schedule.StartDate,
        schedule.EndDate,
        schedule.IsStopped,
        schedule.CreatedBy.Value,
        schedule.LastModifiedBy.Value);
}
