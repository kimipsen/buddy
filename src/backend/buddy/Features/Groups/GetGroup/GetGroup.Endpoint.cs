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
            var result = await bus.InvokeAsync<Result<GroupWithMemberDetails>>(GetGroup.FromClaims(principal, new GroupId(groupId)), cancellationToken);

            return result switch
            {
                Result<GroupWithMemberDetails>.Success(var details) => TypedResults.Ok(GroupResponse.FromGroup(details)),
                Result<GroupWithMemberDetails>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden or Validation, so these are unreachable today
                // -- collapsed to NotFound since this route declares no other status for them.
                Result<GroupWithMemberDetails>.Forbidden => TypedResults.NotFound(),
                Result<GroupWithMemberDetails>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetGroup");

        return groups;
    }
}

public sealed record GroupMemberResponse(Guid UserId, string GivenName, string FamilyName, GroupRole Role, bool IsChild);

public sealed record GroupResponse(
    GroupId Id,
    string Name,
    IReadOnlyCollection<GroupMemberResponse> Members,
    IReadOnlyDictionary<GroupRole, CalendarRole> CalendarPermissionPolicy,
    IReadOnlyDictionary<GroupRole, MealplanAccessTier> MealplanPermissionPolicy,
    IReadOnlyDictionary<GroupRole, MedicineAccessTier> MedicinePermissionPolicy)
{
    public static GroupResponse FromGroup(GroupWithMemberDetails details) => new(
        details.Group.Id,
        details.Group.Name,
        [.. details.Members.Select(m => new GroupMemberResponse(m.UserId.Value, m.Name.GivenName, m.Name.FamilyName, m.Role, m.IsChild))],
        details.Group.CalendarPermissionPolicy,
        details.Group.MealplanPermissionPolicy,
        details.Group.MedicinePermissionPolicy);
}
