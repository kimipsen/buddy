using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class UpdateCurrentCallingNameEndpoint
{
    public static RouteGroupBuilder MapUpdateCurrentCallingName(this RouteGroupBuilder users)
    {
        users.MapPatch("/me/calling-name", async Task<Results<Ok<UserResponse>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            UpdateCallingNameRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // Surrounding whitespace never matters on screen; trimming here also makes "  " clear it.
            var command = UpdateCallingName.FromClaims(principal, request.CallingName?.Trim() ?? "");

            var result = await bus.InvokeAsync<Result<User>>(command, cancellationToken);

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.NotFound => TypedResults.NotFound(),
                Result<User>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateCallingNameHandler never produces Forbidden -- collapsed to NotFound since
                // this route declares no other status for it.
                Result<User>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCurrentCallingName");

        return users;
    }
}

public sealed record UpdateCallingNameRequest(string? CallingName);
