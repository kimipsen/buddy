using System.Security.Claims;

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

            var user = await bus.InvokeAsync<User?>(command, cancellationToken);

            if (user is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(new UserResponse(
                user.Id,
                user.KeycloakSubject,
                user.Email,
                user.UserName,
                user.Name));
        })
        .WithName("UpdateCurrentName");

        return users;
    }
}

public sealed record UpdateNameRequest(string GivenName, string FamilyName);
