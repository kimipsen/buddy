using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Medicines;

public static class SetMedicineEffectInclusionEndpoint
{
    public static RouteGroupBuilder MapSetMedicineEffectInclusion(this RouteGroupBuilder medicines)
    {
        medicines.MapPut("/children/{childId:guid}/schedules/{medicineId:guid}/effect-inclusion", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid medicineId,
            SetMedicineEffectInclusionRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = SetMedicineEffectInclusion.FromClaims(principal, new UserId(childId), new MedicineId(medicineId), request.Included);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // SetMedicineEffectInclusionHandler never produces Validation (the body is one
                // bool), so this collapses to NotFound like StopMedicineSchedule's.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("SetMedicineEffectInclusion");

        return medicines;
    }
}

public sealed record SetMedicineEffectInclusionRequest(bool Included);
