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

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.NotFound => TypedResults.NotFound(),
                // UpdateEmailHandler never produces Forbidden -- collapsed to NotFound since this
                // route declares no other status for it.
                Result<User>.Forbidden => TypedResults.NotFound(),
                // UpdateEmailHandler never produces Validation, but BadRequest is already part of
                // this route's declared results (used above), so map it there if it ever did.
                Result<User>.Validation(var message) => TypedResults.BadRequest(message),
            };
        })
        .WithName("UpdateCurrentEmail");

        return users;
    }
}

public sealed record UpdateEmailRequest(string Email);
