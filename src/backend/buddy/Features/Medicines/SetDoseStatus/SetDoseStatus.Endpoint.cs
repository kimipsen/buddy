using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class SetDoseStatusEndpoint
{
    public static RouteGroupBuilder MapSetDoseStatus(this RouteGroupBuilder medicines)
    {
        medicines.MapPut("/children/{childId:guid}/doses/{medicineId:guid}", async Task<Results<Ok<MedicineDoseOccurrence>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid medicineId,
            DateOnly date,
            TimeOnly time,
            SetDoseStatusRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetDoseStatus.FromClaims(principal, new UserId(childId), new MedicineId(medicineId), date, time, request.Status);
            var result = await bus.InvokeAsync<Result<MedicineDoseOccurrence>>(command, cancellationToken);

            return result switch
            {
                Result<MedicineDoseOccurrence>.Success(var occurrence) => TypedResults.Ok(occurrence),
                Result<MedicineDoseOccurrence>.Forbidden => TypedResults.Forbid(),
                Result<MedicineDoseOccurrence>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MedicineDoseOccurrence>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetDoseStatus");

        return medicines;
    }
}

public sealed record SetDoseStatusRequest(DoseStatus Status);
