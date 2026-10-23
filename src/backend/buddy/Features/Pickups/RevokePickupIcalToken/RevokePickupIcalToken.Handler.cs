using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Pickups;

public static class RevokePickupIcalTokenHandler
{
    public static async Task<Result<Unit>> Handle(
        RevokePickupIcalToken command,
        IPickupFeedEventStore feeds,
        IGuardianLinkEventStore guardians,
        ILogger<RevokePickupIcalToken> logger,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var access = await PickupAuthorization.CheckOwnFeed(userId, guardians, cancellationToken);

        if (access != PickupAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var feedId = PickupFeedId.ForGuardian(userId);
        var feed = PickupFeed.Rehydrate(await feeds.ReadAsync(feedId, cancellationToken));

        // Idempotent: revoking an unknown or already revoked token is a no-op, as in
        // RevokeMealPlanIcalTokenHandler.
        if (feed is null || !feed.Tokens.ContainsKey(command.TokenId))
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await feeds.AppendAsync(feedId, [new PickupIcalTokenRevoked(feedId, command.TokenId, time.GetUtcNow())], cancellationToken);

        logger.PickupIcalTokenRevoked(command.TokenId.Value, userId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}
