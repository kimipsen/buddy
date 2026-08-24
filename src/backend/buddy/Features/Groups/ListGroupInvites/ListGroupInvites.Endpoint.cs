using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class ListGroupInvitesEndpoint
{
    public static RouteGroupBuilder MapListGroupInvites(this RouteGroupBuilder groups)
    {
        groups.MapGet("/{groupId:guid}/invites", async Task<Results<Ok<IReadOnlyCollection<GroupInviteResponse>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListGroupInvites.FromClaims(principal, new GroupId(groupId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<GroupInviteDocument>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<GroupInviteDocument>>.Success(var invites) => TypedResults.Ok<IReadOnlyCollection<GroupInviteResponse>>(
                    [.. invites.Select(i => new GroupInviteResponse(i.Id, i.InvitedEmail, i.Role, i.CreatedAt, i.ExpiresAt))]),
                Result<IReadOnlyCollection<GroupInviteDocument>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<GroupInviteDocument>>.NotFound => TypedResults.NotFound(),
                // ListGroupInvitesHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like RemoveGroupMember does.
                Result<IReadOnlyCollection<GroupInviteDocument>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListGroupInvites");

        return groups;
    }
}
