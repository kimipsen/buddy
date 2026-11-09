using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class SetNightOverridesEndpoint
{
    public static RouteGroupBuilder MapSetNightOverrides(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapPut("/children/{childId:guid}/overrides", async Task<Results<Ok<IReadOnlyCollection<Night>>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            SetNightOverridesRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // A null element can't reach the validator (it would throw building the domain record).
            if (request.Nights.Any(n => n is null || (n.Entry is not null && n.Entry.Responsible is null)))
            {
                return TypedResults.BadRequest(Common.Validation.ValidationProblem.Of("nights must not contain null entries.").ToEnvelope(httpContext));
            }

            var command = SetNightOverrides.FromClaims(
                principal,
                new UserId(childId),
                [.. request.Nights.Select(n => new NightOverrideItem(
                    n.Date,
                    n.Entry is { } entry ? new NightOverride.Planned(entry.ToDomain()) : new NightOverride.Cleared()))]);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<Night>>>(command, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<Night>>.Success(var nights) => TypedResults.Ok(nights),
                Result<IReadOnlyCollection<Night>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<Night>>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IReadOnlyCollection<Night>>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetNightOverrides");

        return nightDuty;
    }
}

public sealed record SetNightOverridesRequest(IReadOnlyList<NightOverrideRequest> Nights);

// Entry null = "nothing planned this night" (NightOverride.Cleared) -- the wire's convention, like
// SetWorkLocationOverrides' locationId null = day off. To follow the fixed schedule again, clear the
// override with ClearNightOverrides instead.
public sealed record NightOverrideRequest(DateOnly Date, NightEntryDto? Entry = null);
