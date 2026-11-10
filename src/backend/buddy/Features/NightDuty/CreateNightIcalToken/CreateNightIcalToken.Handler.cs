using buddy.Common;
using buddy.Common.Security;
using buddy.Features.Guardians;

namespace buddy.Features.NightDuty;

public static class CreateNightIcalTokenHandler
{
    public static async Task<Result<IssuedNightIcalToken>> Handle(
        CreateNightIcalToken command,
        INightFeedEventStore feeds,
        IGuardianLinkEventStore guardians,
        ILogger<CreateNightIcalToken> logger,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // Manage: only an active guardian of the child may subscribe to their nights. The child
        // themself gets Forbidden, anyone else NotFound.
        var access = await NightDutyAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<IssuedNightIcalToken>();
        }

        var (token, hash) = ShareSecret.Generate();
        var tokenId = IcalTokenId.New();
        var feedId = NightFeedId.ForGuardian(command.UserId);
        var now = time.GetUtcNow();
        var issued = new NightIcalTokenIssued(feedId, tokenId, command.ChildId, hash, now);

        // Lazily started with the first token, like PickupFeedStarted.
        if (NightFeed.Rehydrate(await feeds.ReadAsync(feedId, cancellationToken)) is null)
        {
            await feeds.CreateAsync(feedId, [new NightFeedStarted(feedId, command.UserId, now), issued], cancellationToken);
        }
        else
        {
            await feeds.AppendAsync(feedId, [issued], cancellationToken);
        }

        logger.NightIcalTokenIssued(tokenId.Value, command.ChildId.Value, command.UserId.Value);

        return new Result<IssuedNightIcalToken>.Success(new IssuedNightIcalToken(tokenId, token, feedId, now));
    }
}
