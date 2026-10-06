using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class CommitMealPlanImportEndpoint
{
    // Create-style POST: IdempotencyKeyMiddleware replays the first response for a repeated
    // Idempotency-Key, so a double-clicked "Import" can't create every new meal twice.
    public static RouteGroupBuilder MapCommitMealPlanImport(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/imports", async Task<Results<Ok<MealPlanImportResult>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            CommitMealPlanImportRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // RespectNullableAnnotations doesn't cover collection elements (see ReplaceWorkPattern).
            if (request.Entries.Any(e => e is null))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("entries must not contain null entries.").ToEnvelope(httpContext));
            }

            var command = CommitMealPlanImport.FromClaims(principal, new UserId(childId), request.Format, request.ToEntries(), request.ArchiveSingleUse);
            var result = await bus.InvokeAsync<Result<MealPlanImportResult>>(command, cancellationToken);

            return result switch
            {
                Result<MealPlanImportResult>.Success(var imported) => TypedResults.Ok(imported),
                Result<MealPlanImportResult>.Forbidden => TypedResults.Forbid(),
                Result<MealPlanImportResult>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MealPlanImportResult>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("CommitMealPlanImport");

        return mealplans;
    }

    public static RouteGroupBuilder MapCommitMealPlanImportForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/groups/{groupId:guid}/imports", async Task<Results<Ok<MealPlanImportResult>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            CommitMealPlanImportRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // RespectNullableAnnotations doesn't cover collection elements (see ReplaceWorkPattern).
            if (request.Entries.Any(e => e is null))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("entries must not contain null entries.").ToEnvelope(httpContext));
            }

            var command = CommitMealPlanImportForGroup.FromClaims(principal, new GroupId(groupId), request.Format, request.ToEntries(), request.ArchiveSingleUse);
            var result = await bus.InvokeAsync<Result<MealPlanImportResult>>(command, cancellationToken);

            return result switch
            {
                Result<MealPlanImportResult>.Success(var imported) => TypedResults.Ok(imported),
                Result<MealPlanImportResult>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<MealPlanImportResult>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy grants View but not Manage.
                Result<MealPlanImportResult>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("CommitMealPlanImportForGroup");

        return mealplans;
    }
}

public sealed record CommitMealPlanImportEntryRequest(DateOnly Date, MealSlot Slot, Guid? MealId = null, string? NewMealName = null, string? Notes = null);

public sealed record CommitMealPlanImportRequest(string Format, IReadOnlyList<CommitMealPlanImportEntryRequest> Entries, bool ArchiveSingleUse = true)
{
    public IReadOnlyList<MealPlanImportEntry> ToEntries() =>
    [
        .. Entries.Select(e => new MealPlanImportEntry(
            e.Date,
            e.Slot,
            e.MealId is { } id ? new MealId(id) : null,
            FreeText.Normalize(e.NewMealName),
            FreeText.Normalize(e.Notes)))
    ];
}
