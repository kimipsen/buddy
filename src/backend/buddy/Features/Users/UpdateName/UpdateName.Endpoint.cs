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

            if (result is not Result<User>.Success(var user))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(UserResponse.FromUser(user));
        })
        .WithName("UpdateCurrentName");

        return users;
    }
}

public sealed record UpdateNameRequest(string GivenName, string FamilyName);
