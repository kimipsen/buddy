using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Idempotent: a family that isn't connected gets Success with no event. Linked recipes stay on
// their meals and come back with a reconnect (docs/backend/analysis/mealie-integration.md, Question 4).
public static class DisconnectMealieHandler
{
    public static async Task<Result<Unit>> Handle(
        DisconnectMealie command,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        ILogger<DisconnectMealie> logger,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var id = await MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync(command.ChildId, guardians, connections, cancellationToken);

        if (id is null || MealieConnection.Rehydrate(await connections.ReadAsync(id, cancellationToken))?.Current is null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await connections.AppendAsync(id, [new MealieDisconnected(id, command.UserId, time.GetUtcNow())], cancellationToken);

        logger.MealieDisconnected(command.ChildId.Value, command.UserId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}
