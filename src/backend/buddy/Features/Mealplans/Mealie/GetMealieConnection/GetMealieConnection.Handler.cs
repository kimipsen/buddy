using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Makes no Mealie call: what Buddy stored at the last connect.
public static class GetMealieConnectionHandler
{
    public static async Task<Result<MealieConnectionView>> Handle(
        GetMealieConnection query,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<MealieConnectionView>();
        }

        var id = await MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync(query.ChildId, guardians, connections, cancellationToken);
        var connection = id is null ? null : await connections.FindSnapshotAsync(id, cancellationToken);

        return new Result<MealieConnectionView>.Success(MealieConnectionView.From(connection));
    }
}
