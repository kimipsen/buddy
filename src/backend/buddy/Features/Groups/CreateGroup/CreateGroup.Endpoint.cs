using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class CreateGroupEndpoint
{
    public static RouteGroupBuilder MapCreateGroup(this RouteGroupBuilder groups)
    {
        groups.MapPost("/", async Task<Results<Ok<GroupResponse>, UnauthorizedHttpResult>> (
            ClaimsPrincipal principal,
            CreateGroupRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateGroup.FromClaims(principal, request.Name);
            var result = await bus.InvokeAsync<CreateGroupResult>(command, cancellationToken);

            if (result.Unauthenticated)
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.Ok(GroupResponse.FromGroup(result.Group!));
        })
        .WithName("CreateGroup");

        return groups;
    }
}

public sealed record CreateGroupRequest(string Name);
