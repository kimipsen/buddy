using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class GetMyFeelingsTodayEndpoint
{
    public static RouteGroupBuilder MapGetMyFeelingsToday(this RouteGroupBuilder feelings)
    {
        feelings.MapGet("/me/today", async Task<Results<Ok<MyFeelingsTodayResponse>, NotFound>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<MyFeelingsTodayResponse>>(GetMyFeelingsToday.FromClaims(principal), cancellationToken);

            return result switch
            {
                Result<MyFeelingsTodayResponse>.Success(var today) => TypedResults.Ok(today),
                Result<MyFeelingsTodayResponse>.NotFound => TypedResults.NotFound(),
                // CheckSelf has no Forbidden outcome, and there are no validation rules.
                Result<MyFeelingsTodayResponse>.Forbidden => TypedResults.NotFound(),
                Result<MyFeelingsTodayResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetMyFeelingsToday");

        return feelings;
    }
}
