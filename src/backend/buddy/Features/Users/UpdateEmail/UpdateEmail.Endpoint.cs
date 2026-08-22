using System.Security.Claims;

using buddy.Common;

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

            var result = await bus.InvokeAsync<Result<User>>(command, cancellationToken);

            if (result is not Result<User>.Success(var user))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(UserResponse.FromUser(user));
        })
        .WithName("UpdateCurrentEmail");

        return users;
    }
}

public sealed record UpdateEmailRequest(string Email);
