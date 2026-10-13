using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class LogFeelingCheckInEndpoint
{
    public static RouteGroupBuilder MapLogFeelingCheckIn(this RouteGroupBuilder feelings)
    {
        feelings.MapPost("/children/{childId:guid}/check-ins", async Task<Results<Ok<FeelingCheckInResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            LogFeelingCheckInRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = LogFeelingCheckIn.FromClaims(principal, new UserId(childId), request);
            var result = await bus.InvokeAsync<Result<FeelingCheckInResponse>>(command, cancellationToken);

            return result switch
            {
                Result<FeelingCheckInResponse>.Success(var checkIn) => TypedResults.Ok(checkIn),
                Result<FeelingCheckInResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<FeelingCheckInResponse>.NotFound => TypedResults.NotFound(),
                Result<FeelingCheckInResponse>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("LogFeelingCheckIn");

        return feelings;
    }
}

public sealed record LogFeelingCheckInRequest(
    FeelingZone Zone,
    IReadOnlyList<FeelingTag>? Tags = null,
    string? Note = null,
    DateOnly? Date = null,
    TimeOnly? Time = null);
