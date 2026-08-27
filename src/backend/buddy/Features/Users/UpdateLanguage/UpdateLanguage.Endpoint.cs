using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class UpdateCurrentLanguageEndpoint
{
    public static RouteGroupBuilder MapUpdateCurrentLanguage(this RouteGroupBuilder users)
    {
        users.MapPatch("/me/language", async Task<Results<Ok<UserResponse>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            UpdateLanguageRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateLanguage.FromClaims(principal, new Language(request.Language));

            var result = await bus.InvokeAsync<Result<User>>(command, cancellationToken);

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.NotFound => TypedResults.NotFound(),
                Result<User>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateLanguageHandler never produces Forbidden -- collapsed to NotFound since
                // this route declares no other status for it.
                Result<User>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCurrentLanguage");

        return users;
    }
}

public sealed record UpdateLanguageRequest(string Language);
