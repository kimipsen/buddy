using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class UpdateCurrentTimerPreferencesEndpoint
{
    public static RouteGroupBuilder MapUpdateCurrentTimerPreferences(this RouteGroupBuilder users)
    {
        // An unbindable body (missing fields, an unknown enum name, a non-boolean switch) never
        // reaches the handler: RequestBindingFailureMiddleware renders it as the 400 declared here.
        users.MapPatch("/me/timer-preferences", async Task<Results<Ok<UserResponse>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            UpdateTimerPreferencesRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateTimerPreferences.FromClaims(principal, new TimerPreferences(
                request.Shape, request.Size, request.Color, request.ShowMinutes, request.Chime, request.ChimeSound, request.ChimeOnHome));

            var result = await bus.InvokeAsync<Result<User>>(command, cancellationToken);

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.NotFound => TypedResults.NotFound(),
                Result<User>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateTimerPreferencesHandler never produces Forbidden -- collapsed to NotFound
                // since this route declares no other status for it.
                Result<User>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCurrentTimerPreferences");

        return users;
    }
}

public sealed record UpdateTimerPreferencesRequest(
    TimerShape Shape,
    TimerSize Size,
    TimerColor Color,
    bool ShowMinutes,
    bool Chime,
    ChimeSound ChimeSound,
    bool ChimeOnHome);
