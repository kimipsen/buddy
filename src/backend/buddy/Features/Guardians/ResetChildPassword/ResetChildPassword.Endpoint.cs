using System.Security.Claims;

using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class ResetChildPasswordEndpoint
{
    public static RouteGroupBuilder MapResetChildPassword(this RouteGroupBuilder children)
    {
        children.MapPost("/{childId:guid}/password-reset", async Task<Results<Ok<ChildPasswordResetResponse>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<ResetChildPasswordOutcome>(
                ResetChildPassword.FromClaims(principal, new UserId(childId)), cancellationToken);

            return outcome switch
            {
                ResetChildPasswordOutcome.Success(var username, var temporaryPassword) =>
                    TypedResults.Ok(new ChildPasswordResetResponse(username, temporaryPassword)),
                ResetChildPasswordOutcome.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ResetChildPassword");

        return children;
    }
}

// Shown exactly once, like CreateChild's ChildResponse: the password is never stored or logged.
public sealed record ChildPasswordResetResponse(string Username, string TemporaryPassword);
