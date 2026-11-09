using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class ReplaceNightPatternEndpoint
{
    public static RouteGroupBuilder MapReplaceNightPattern(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapPut("/children/{childId:guid}/pattern", async Task<Results<Ok<NightScheduleResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            ReplaceNightPatternRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // A null element can't reach the validator (it would throw building the domain record).
            // RespectNullableAnnotations doesn't cover collection elements, so this stays.
            if (request.Days.Any(d => d is null || d.Entry is null || d.Entry.Responsible is null))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("days must not contain null entries.").ToEnvelope(httpContext));
            }

            var command = ReplaceNightPattern.FromClaims(
                principal,
                new UserId(childId),
                request.EffectiveFrom,
                request.CycleWeeks,
                request.AnchorMonday,
                [.. request.Days.Select(d => new NightPatternDay(d.Week, d.Day, d.Entry.ToDomain()))]);
            var result = await bus.InvokeAsync<Result<NightScheduleResponse>>(command, cancellationToken);

            return result switch
            {
                Result<NightScheduleResponse>.Success(var response) => TypedResults.Ok(response),
                Result<NightScheduleResponse>.Forbidden => TypedResults.Forbid(),
                Result<NightScheduleResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<NightScheduleResponse>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ReplaceNightPattern");

        return nightDuty;
    }
}

// EffectiveFrom is optional: omitted means "from today" in the child's time zone.
public sealed record ReplaceNightPatternRequest(int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<NightPatternDayRequest> Days, DateOnly? EffectiveFrom = null);

public sealed record NightPatternDayRequest(int Week, DayOfWeek Day, NightEntryDto Entry);
