using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.NightDuty;

public static class RevokeNightIcalTokenHandler
{
    public static async Task<Result<Unit>> Handle(
        RevokeNightIcalToken command,
        INightFeedEventStore feeds,
        IGuardianLinkEventStore guardians,
        ILogger<RevokeNightIcalToken> logger,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await NightDutyAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var feedId = NightFeedId.ForGuardian(command.UserId);
        var feed = NightFeed.Rehydrate(await feeds.ReadAsync(feedId, cancellationToken));

        // Idempotent: revoking an unknown or already revoked token, or one issued for another child,
        // is a no-op, as in RevokePickupIcalTokenHandler.
        if (feed is null || !feed.Tokens.TryGetValue(command.TokenId, out var info) || info.ChildId != command.ChildId)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await feeds.AppendAsync(feedId, [new NightIcalTokenRevoked(feedId, command.TokenId, time.GetUtcNow())], cancellationToken);

        logger.NightIcalTokenRevoked(command.TokenId.Value, command.UserId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}
