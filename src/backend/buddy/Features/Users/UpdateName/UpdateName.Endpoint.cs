using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class UpdateCurrentNameEndpoint
{
    public static RouteGroupBuilder MapUpdateCurrentName(this RouteGroupBuilder users)
    {
        users.MapPatch("/me/name", async Task<Results<Ok<UserResponse>, NotFound>> (
            ClaimsPrincipal principal,
            UpdateNameRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateName.FromClaims(principal, Name.New(request.GivenName, request.FamilyName));

            var result = await bus.InvokeAsync<Result<User>>(command, cancellationToken);

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.NotFound => TypedResults.NotFound(),
                // UpdateNameHandler never produces Forbidden or Validation -- collapsed to
                // NotFound since this route declares no other status for them.
                Result<User>.Forbidden => TypedResults.NotFound(),
                Result<User>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCurrentName");

        return users;
    }
}

public sealed record UpdateNameRequest(string GivenName, string FamilyName);
