using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class ListMedicineEffectNotesEndpoint
{
    public static RouteGroupBuilder MapListMedicineEffectNotes(this RouteGroupBuilder medicines)
    {
        medicines.MapGet("/children/{childId:guid}/effect-notes", async Task<Results<Ok<MedicineEffectNotesResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = ListMedicineEffectNotes.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<MedicineEffectNotesResponse>>(query, cancellationToken);

            return result switch
            {
                Result<MedicineEffectNotesResponse>.Success(var notes) => TypedResults.Ok(notes),
                Result<MedicineEffectNotesResponse>.Forbidden => TypedResults.Forbid(),
                Result<MedicineEffectNotesResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MedicineEffectNotesResponse>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ListMedicineEffectNotes");

        return medicines;
    }
}
