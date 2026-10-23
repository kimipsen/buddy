using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Pickups;

public static class ListPickupIcalTokensHandler
{
    public static async Task<Result<IReadOnlyCollection<PickupIcalTokenSummary>>> Handle(
        ListPickupIcalTokens query,
        IPickupFeedEventStore feeds,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await PickupAuthorization.CheckOwnFeed(query.UserId, guardians, cancellationToken);

        if (access != PickupAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<PickupIcalTokenSummary>>();
        }

        // No stream yet means no token was ever issued.
        if (await feeds.FindSnapshotAsync(PickupFeedId.ForGuardian(query.UserId), cancellationToken) is not { } feed)
        {
            return new Result<IReadOnlyCollection<PickupIcalTokenSummary>>.Success([]);
        }

        var tokens = feed.Tokens
            .Select(kv => new PickupIcalTokenSummary(kv.Key.Value, kv.Value.IssuedAt))
            .OrderByDescending(token => token.IssuedAt)
            .ToArray();

        return new Result<IReadOnlyCollection<PickupIcalTokenSummary>>.Success(tokens);
    }
}
