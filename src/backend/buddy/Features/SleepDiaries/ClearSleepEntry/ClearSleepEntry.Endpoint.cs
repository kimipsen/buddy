using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class ClearSleepEntryEndpoint
{
    public static RouteGroupBuilder MapClearSleepEntry(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapDelete("/children/{childId:guid}/entries/{date}", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ClearSleepEntry.FromClaims(principal, new UserId(childId), date);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ClearSleepEntryHandler never produces Forbidden (guardian-only feature) or
                // Validation (no structural rules) -- collapsed to NotFound.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ClearSleepEntry");

        return sleepDiary;
    }
}
