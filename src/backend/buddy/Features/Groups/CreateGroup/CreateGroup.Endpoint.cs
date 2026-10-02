using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class CreateGroupEndpoint
{
    public static RouteGroupBuilder MapCreateGroup(this RouteGroupBuilder groups)
    {
        groups.MapPost("/", async Task<Ok<GroupResponse>> (
            ClaimsPrincipal principal,
            CreateGroupRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreateGroup.FromClaims(principal, request.Name);
            var group = await bus.InvokeAsync<GroupWithMemberDetails>(command, cancellationToken);

            return TypedResults.Ok(GroupResponse.FromGroup(group));
        })
        .WithName("CreateGroup");

        return groups;
    }
}

public sealed record CreateGroupRequest(string Name);
