using System.Security.Claims;

using buddy.Features.Calendars;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class GetGroupEndpoint
{
    public static RouteGroupBuilder MapGetGroup(this RouteGroupBuilder groups)
    {
        groups.MapGet("/{groupId:guid}", async Task<Results<Ok<GroupResponse>, NotFound>> (
            ClaimsPrincipal principal,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<GetGroupResult>(GetGroup.FromClaims(principal, new GroupId(groupId)), cancellationToken);

            if (result.Group is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(GroupResponse.FromGroup(result.Group));
        })
        .WithName("GetGroup");

        return groups;
    }
}

public sealed record GroupMemberResponse(Guid UserId, GroupRole Role);

public sealed record GroupResponse(GroupId Id, string Name, IReadOnlyCollection<GroupMemberResponse> Members, IReadOnlyDictionary<GroupRole, CalendarRole> CalendarPermissionPolicy)
{
    public static GroupResponse FromGroup(Group group) => new(
        group.Id,
        group.Name,
        [.. group.Members.Select(m => new GroupMemberResponse(m.Key.Value, m.Value))],
        group.CalendarPermissionPolicy);
}
