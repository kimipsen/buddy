using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class UpdateCurrentEmailEndpoint
{
    public static RouteGroupBuilder MapUpdateCurrentEmail(this RouteGroupBuilder users)
    {
        users.MapPatch("/me/email", async Task<Results<Ok<UserResponse>, BadRequest<string>, NotFound>> (
            ClaimsPrincipal principal,
            UpdateEmailRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return TypedResults.BadRequest($"The '{nameof(request.Email)}' field is required.");
            }

            var command = UpdateEmail.FromClaims(principal, request.Email);

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
        .WithName("UpdateCurrentEmail");

        return users;
    }
}

public sealed record UpdateEmailRequest(string Email);
