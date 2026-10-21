using System.Security.Claims;

using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class PreviewMealieImportEndpoint
{
    // POST like PreviewMealPlanImport; it writes nothing.
    public static RouteGroupBuilder MapPreviewMealieImport(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/imports/mealie/preview", async (
            ClaimsPrincipal principal,
            Guid childId,
            PreviewMealieImportRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = PreviewMealieImport.FromClaims(principal, new UserId(childId), request.From, request.To);
            var outcome = await bus.InvokeAsync<MealieOutcome<MealieImportPreview>>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .ProducesMealieErrors()
        .WithName("PreviewMealieImport");

        return mealplans;
    }
}

public sealed record PreviewMealieImportRequest(DateOnly From, DateOnly To);
