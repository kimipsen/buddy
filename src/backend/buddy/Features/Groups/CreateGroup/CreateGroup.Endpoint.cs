using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class CreateGroupEndpoint
{
    public static RouteGroupBuilder MapCreateGroup(this RouteGroupBuilder groups)
    {
        groups.MapPost("/", async Task<Results<Ok<GroupResponse>, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            CreateGroupRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateGroup.FromClaims(principal, request.Name.Trim());
            var result = await bus.InvokeAsync<CreateGroupOutcome>(command, cancellationToken);

            return result switch
            {
                CreateGroupOutcome.Success(var group) => TypedResults.Ok(GroupResponse.FromGroup(group)),
                CreateGroupOutcome.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("CreateGroup");

        return groups;
    }
}

public sealed record CreateGroupRequest(string Name);
