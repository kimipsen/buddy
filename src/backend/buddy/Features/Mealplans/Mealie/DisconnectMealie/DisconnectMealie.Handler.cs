using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Idempotent: a family that isn't connected gets Success with no event. Every connected stream of
// the family is disconnected, not only the one in use (MealieFamilyConnections). Linked recipes
// stay on their meals and come back with a reconnect (docs/backend/analysis/mealie-integration.md,
// Question 4).
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

        await MealieFamilyConnections.DisconnectAllAsync(command.ChildId, command.UserId, guardians, connections, logger, time.GetUtcNow(), cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
