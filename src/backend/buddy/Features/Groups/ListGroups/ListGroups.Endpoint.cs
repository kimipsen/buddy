using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class ListGroupsEndpoint
{
    public static RouteGroupBuilder MapListGroups(this RouteGroupBuilder groups)
    {
        groups.MapGet("/", async Task<Ok<IReadOnlyCollection<GroupSummaryResponse>>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var memberships = await bus.InvokeAsync<IReadOnlyCollection<GroupMembershipDocument>>(ListGroups.FromClaims(principal), cancellationToken);

            return TypedResults.Ok<IReadOnlyCollection<GroupSummaryResponse>>(
                [.. memberships.Select(m => new GroupSummaryResponse(new GroupId(m.GroupId), m.GroupName, m.Role))]);
        })
        .WithName("ListGroups");

        return groups;
    }
}

public sealed record GroupSummaryResponse(GroupId Id, string Name, GroupRole Role);
