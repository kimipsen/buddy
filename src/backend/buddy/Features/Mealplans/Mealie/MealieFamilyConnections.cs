using buddy.Features.Guardians;
using buddy.Features.Users;

using JasperFx;

namespace buddy.Features.Mealplans;

public sealed record FamilyMealieConnection(MealieConnectionId Id, IReadOnlyCollection<MealieConnectionEvent> Events, DateTimeOffset? ConnectedAt)
{
    public bool IsConnected => ConnectedAt is not null;
}

// One family, one Mealie. A family can end up with more than one connection stream: two first
// connects at the same moment each start one (there's no family document a constraint could guard),
// and two families can merge through a new guardian link. Every connect therefore disconnects the
// family's other connected streams, and a disconnect disconnects them all, so the family never
// falls back to a stream it didn't choose. See docs/backend/analysis/mealie-integration.md, Question 2.
public static class MealieFamilyConnections
{
    // After a connect: every connected stream except the one the family uses first. Two racing
    // connects each run this after their own write, see the same order and disconnect the same
    // streams, so exactly one stays connected.
    public static async Task DisconnectAllButPreferredAsync(UserId childId, UserId by, IGuardianLinkEventStore guardians, IMealieConnectionEventStore connections, ILogger logger, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await MealFamilyResolution.ListFamilyMealieConnectionsAsync(childId, guardians, connections, cancellationToken);

        await DisconnectAsync(family.Skip(1), childId, by, connections, logger, now, tolerateRaces: true, cancellationToken);
    }

    // A guardian's disconnect.
    public static async Task DisconnectAllAsync(UserId childId, UserId by, IGuardianLinkEventStore guardians, IMealieConnectionEventStore connections, ILogger logger, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await MealFamilyResolution.ListFamilyMealieConnectionsAsync(childId, guardians, connections, cancellationToken);

        await DisconnectAsync(family, childId, by, connections, logger, now, tolerateRaces: false, cancellationToken);
    }

    private static async Task DisconnectAsync(IEnumerable<FamilyMealieConnection> family, UserId childId, UserId by, IMealieConnectionEventStore connections, ILogger logger, DateTimeOffset now, bool tolerateRaces, CancellationToken cancellationToken)
    {
        foreach (var connection in family.Where(c => c.IsConnected))
        {
            try
            {
                await connections.AppendAsync(connection.Id, [new MealieDisconnected(connection.Id, by, now)], cancellationToken);
                logger.MealieDisconnected(childId.Value, by.Value);
            }
            catch (ConcurrencyException) when (tolerateRaces)
            {
                // A racing connect or disconnect wrote to this stream since it was read; that
                // request decides it. An explicit disconnect lets it through as a 409 instead.
            }
        }
    }
}
