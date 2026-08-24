using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ArchiveMealForGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        ArchiveMealForGroup command,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, Unit>();
        }

        return await ArchiveMealHandler.ArchiveForChildAsync(access.AnchorChildId, command.MealId, command.UserId!, meals, guardians, cancellationToken);
    }
}
