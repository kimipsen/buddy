using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class SetDoseStatusForGroupEndpoint
{
    public static RouteGroupBuilder MapSetDoseStatusForGroup(this RouteGroupBuilder medicines)
    {
        medicines.MapPut("/groups/{groupId:guid}/children/{childId:guid}/doses/{medicineId:guid}", async Task<Results<Ok<MedicineDoseOccurrence>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid childId,
            Guid medicineId,
            DateOnly date,
            TimeOnly time,
            SetDoseStatusRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetDoseStatusForGroup.FromClaims(principal, new GroupId(groupId), new UserId(childId), new MedicineId(medicineId), date, time, request.Status);
            var result = await bus.InvokeAsync<Result<MedicineDoseOccurrence>>(command, cancellationToken);

            return result switch
            {
                Result<MedicineDoseOccurrence>.Success(var occurrence) => TypedResults.Ok(occurrence),
                Result<MedicineDoseOccurrence>.Forbidden => TypedResults.Forbid(),
                Result<MedicineDoseOccurrence>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MedicineDoseOccurrence>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetDoseStatusForGroup");

        return medicines;
    }
}
