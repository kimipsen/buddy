using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Medicines;
using buddy.Features.Mealplans;

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
            var result = await bus.InvokeAsync<Result<Group>>(GetGroup.FromClaims(principal, new GroupId(groupId)), cancellationToken);

            return result switch
            {
                Result<Group>.Success(var group) => TypedResults.Ok(GroupResponse.FromGroup(group)),
                Result<Group>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden or Validation, so these are unreachable today
                // -- collapsed to NotFound since this route declares no other status for them.
                Result<Group>.Forbidden => TypedResults.NotFound(),
                Result<Group>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetGroup");

        return groups;
    }
}

public sealed record GroupMemberResponse(Guid UserId, GroupRole Role);

public sealed record GroupResponse(
    GroupId Id,
    string Name,
    IReadOnlyCollection<GroupMemberResponse> Members,
    IReadOnlyDictionary<GroupRole, CalendarRole> CalendarPermissionPolicy,
    IReadOnlyDictionary<GroupRole, MealplanAccessTier> MealplanPermissionPolicy,
    IReadOnlyDictionary<GroupRole, MedicineAccessTier> MedicinePermissionPolicy)
{
    public static GroupResponse FromGroup(Group group) => new(
        group.Id,
        group.Name,
        [.. group.Members.Select(m => new GroupMemberResponse(m.Key.Value, m.Value))],
        group.CalendarPermissionPolicy,
        group.MealplanPermissionPolicy,
        group.MedicinePermissionPolicy);
}
