using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class CheckInEndpoint
{
    public static RouteGroupBuilder MapCheckIn(this RouteGroupBuilder feelings)
    {
        feelings.MapPost("/me/check-ins", async Task<Results<Ok<FeelingCheckInResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            CheckInRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CheckIn.FromClaims(principal, request);
            var result = await bus.InvokeAsync<Result<FeelingCheckInResponse>>(command, cancellationToken);

            return result switch
            {
                Result<FeelingCheckInResponse>.Success(var checkIn) => TypedResults.Ok(checkIn),
                Result<FeelingCheckInResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<FeelingCheckInResponse>.NotFound => TypedResults.NotFound(),
                // CheckSelf has no Forbidden outcome: a caller who isn't a child is NotFound.
                Result<FeelingCheckInResponse>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("CheckIn");

        return feelings;
    }
}

public sealed record CheckInRequest(FeelingZone Zone, IReadOnlyList<FeelingTag>? Tags = null, string? Note = null);
