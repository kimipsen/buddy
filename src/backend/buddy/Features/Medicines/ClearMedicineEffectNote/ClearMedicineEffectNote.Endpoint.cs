using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class ClearMedicineEffectNoteEndpoint
{
    public static RouteGroupBuilder MapClearMedicineEffectNote(this RouteGroupBuilder medicines)
    {
        medicines.MapDelete("/children/{childId:guid}/effect-notes/{date}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ClearMedicineEffectNote.FromClaims(principal, new UserId(childId), date);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ClearMedicineEffectNoteHandler has no structural rules -- collapsed to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ClearMedicineEffectNote");

        return medicines;
    }
}
