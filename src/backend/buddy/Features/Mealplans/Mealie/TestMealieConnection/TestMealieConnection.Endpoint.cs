using System.Security.Claims;

using buddy.Common.OpenApi;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class TestMealieConnectionEndpoint
{
    public static RouteGroupBuilder MapTestMealieConnection(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/mealie/test-connection", async (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<MealieOutcome<TestMealieConnectionResult>>(TestMealieConnection.FromClaims(principal, new UserId(childId)), cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        // A failing Mealie is a Failed test result (200), never a 502.
        .ProducesErrorCode(StatusCodes.Status409Conflict, MealieErrorCodes.NotConnected)
        .WithName("TestMealieConnection");

        return mealplans;
    }
}
