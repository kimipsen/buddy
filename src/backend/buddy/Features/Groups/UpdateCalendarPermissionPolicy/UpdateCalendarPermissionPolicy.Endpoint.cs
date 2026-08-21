using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Calendars;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class UpdateCalendarPermissionPolicyEndpoint
{
    public static RouteGroupBuilder MapUpdateCalendarPermissionPolicy(this RouteGroupBuilder groups)
    {
        groups.MapPut("/{groupId:guid}/calendar-permission-policy", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            UpdateCalendarPermissionPolicyRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            foreach (var role in Enum.GetValues<GroupRole>())
            {
                if (!request.Policy.ContainsKey(role))
                {
                    return TypedResults.BadRequest($"The policy must include an entry for every group role; '{role}' is missing.");
                }
            }

            var policy = request.Policy.ToImmutableDictionary();
            var command = UpdateCalendarPermissionPolicy.FromClaims(principal, new GroupId(groupId), policy);
            var access = await bus.InvokeAsync<GroupAccess>(command, cancellationToken);

            return access switch
            {
                GroupAccess.Allowed => TypedResults.NoContent(),
                GroupAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateGroupCalendarPermissionPolicy");

        return groups;
    }
}

public sealed record UpdateCalendarPermissionPolicyRequest(IReadOnlyDictionary<GroupRole, CalendarRole> Policy);
