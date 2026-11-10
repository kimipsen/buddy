using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.NightDuty;

public static class ListNightIcalTokensHandler
{
    public static async Task<Result<IReadOnlyCollection<NightIcalTokenSummary>>> Handle(
        ListNightIcalTokens query,
        INightFeedEventStore feeds,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await NightDutyAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<NightIcalTokenSummary>>();
        }

        // No stream yet means no token was ever issued.
        if (await feeds.FindSnapshotAsync(NightFeedId.ForGuardian(query.UserId), cancellationToken) is not { } feed)
        {
            return new Result<IReadOnlyCollection<NightIcalTokenSummary>>.Success([]);
        }

        var tokens = feed.Tokens
            .Where(kv => kv.Value.ChildId == query.ChildId)
            .Select(kv => new NightIcalTokenSummary(kv.Key.Value, kv.Value.IssuedAt))
            .OrderByDescending(token => token.IssuedAt)
            .ToArray();

        return new Result<IReadOnlyCollection<NightIcalTokenSummary>>.Success(tokens);
    }
}
