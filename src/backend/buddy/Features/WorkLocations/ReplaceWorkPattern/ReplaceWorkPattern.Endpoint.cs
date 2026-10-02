using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class ReplaceWorkPatternEndpoint
{
    public static RouteGroupBuilder MapReplaceWorkPattern(this RouteGroupBuilder workLocations)
    {
        workLocations.MapPut("/me/pattern", async Task<Results<Ok<WorkPatternResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            ReplaceWorkPatternRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // A null element can't reach the validator (it would throw building the domain record).
            if (request.Days?.Any(d => d is null) == true)
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("days must not contain null entries.").ToEnvelope(httpContext));
            }

            var pattern = new WorkPattern(
                request.CycleWeeks,
                request.AnchorMonday,
                [.. (request.Days ?? []).Select(d => new WorkPatternDay(d.Week, d.Day, new WorkLocationId(d.LocationId)))]);
            var command = ReplaceWorkPattern.FromClaims(principal, pattern);
            var result = await bus.InvokeAsync<Result<WorkPatternResponse>>(command, cancellationToken);

            return result switch
            {
                Result<WorkPatternResponse>.Success(var response) => TypedResults.Ok(response),
                Result<WorkPatternResponse>.Forbidden => TypedResults.Forbid(),
                Result<WorkPatternResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<WorkPatternResponse>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ReplaceWorkPattern");

        return workLocations;
    }
}

public sealed record ReplaceWorkPatternRequest(int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<WorkPatternDayDto>? Days);
