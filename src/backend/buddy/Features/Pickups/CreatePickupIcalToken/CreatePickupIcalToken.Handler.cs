using buddy.Common;
using buddy.Common.Security;
using buddy.Features.Guardians;

namespace buddy.Features.Pickups;

public static class CreatePickupIcalTokenHandler
{
    public static async Task<Result<IssuedPickupIcalToken>> Handle(
        CreatePickupIcalToken command,
        IPickupFeedEventStore feeds,
        IGuardianLinkEventStore guardians,
        ILogger<CreatePickupIcalToken> logger,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var access = await PickupAuthorization.CheckOwnFeed(userId, guardians, cancellationToken);

        if (access != PickupAccess.Allowed)
        {
            return access.ToDeniedResult<IssuedPickupIcalToken>();
        }

        var (token, hash) = ShareSecret.Generate();
        var tokenId = IcalTokenId.New();
        var feedId = PickupFeedId.ForGuardian(userId);
        var now = time.GetUtcNow();
        var issued = new PickupIcalTokenIssued(feedId, tokenId, hash, now);

        // Lazily started with the first token, like PickupScheduleCreated with the first assignment.
        if (PickupFeed.Rehydrate(await feeds.ReadAsync(feedId, cancellationToken)) is null)
        {
            await feeds.CreateAsync(feedId, [new PickupFeedStarted(feedId, userId, now), issued], cancellationToken);
        }
        else
        {
            await feeds.AppendAsync(feedId, [issued], cancellationToken);
        }

        logger.PickupIcalTokenIssued(tokenId.Value, userId.Value);

        return new Result<IssuedPickupIcalToken>.Success(new IssuedPickupIcalToken(tokenId, token, feedId));
    }
}
