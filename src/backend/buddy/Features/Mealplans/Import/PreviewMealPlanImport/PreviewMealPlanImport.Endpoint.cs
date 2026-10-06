using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class PreviewMealPlanImportEndpoint
{
    // POST because the text can be far longer than a query string allows; it writes nothing.
    public static RouteGroupBuilder MapPreviewMealPlanImport(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/imports/preview", async Task<Results<Ok<MealPlanImportPreview>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            PreviewMealPlanImportRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = PreviewMealPlanImport.FromClaims(principal, new UserId(childId), request.Text, request.FormatOrAuto, request.ToOptions());
            var result = await bus.InvokeAsync<Result<MealPlanImportPreview>>(command, cancellationToken);

            return result switch
            {
                Result<MealPlanImportPreview>.Success(var preview) => TypedResults.Ok(preview),
                Result<MealPlanImportPreview>.Forbidden => TypedResults.Forbid(),
                Result<MealPlanImportPreview>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MealPlanImportPreview>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("PreviewMealPlanImport");

        return mealplans;
    }

    public static RouteGroupBuilder MapPreviewMealPlanImportForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/groups/{groupId:guid}/imports/preview", async Task<Results<Ok<MealPlanImportPreview>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            PreviewMealPlanImportRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = PreviewMealPlanImportForGroup.FromClaims(principal, new GroupId(groupId), request.Text, request.FormatOrAuto, request.ToOptions());
            var result = await bus.InvokeAsync<Result<MealPlanImportPreview>>(command, cancellationToken);

            return result switch
            {
                Result<MealPlanImportPreview>.Success(var preview) => TypedResults.Ok(preview),
                Result<MealPlanImportPreview>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MealPlanImportPreview>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy grants View but not Manage.
                Result<MealPlanImportPreview>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("PreviewMealPlanImportForGroup");

        return mealplans;
    }
}

public sealed record PreviewMealPlanImportRequest(string Text, string? Format = null, ImportWeekStart? WeekStart = null, MealSlot? Slot = null)
{
    public string FormatOrAuto => string.IsNullOrWhiteSpace(Format) ? MealPlanImportFormats.Auto : Format.Trim();

    public MealPlanImportOptions ToOptions() => new(
        WeekStart ?? MealPlanImportOptions.Default.WeekStart,
        Slot ?? MealPlanImportOptions.Default.Slot);
}
