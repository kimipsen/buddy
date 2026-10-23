using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.RevokePickupIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class RevokePickupIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RevokePickupIcalToken")]
    public async Task A_revoked_token_stops_the_feed_and_leaves_the_other_tokens_working()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var revoked = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);
        var kept = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);

        await RevokeAsync(guardianToken, revoked.TokenId, expectedStatus: 204);

        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url(revoked.SubscriptionPath);
            _.StatusCodeShouldBe(404);
        });
        await PickupTestHelpers.ReadFeedAsync(fixture, kept.SubscriptionPath);
        Assert.Equal([kept.TokenId], (await PickupTestHelpers.ListIcalTokensAsync(fixture, guardianToken)).Select(t => t.TokenId));
    }

    [Fact]
    public async Task Revoking_an_unknown_or_already_revoked_token_is_a_no_op()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        // No feed stream at all yet.
        await RevokeAsync(guardianToken, Guid.CreateVersion7(), expectedStatus: 204);

        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);
        await RevokeAsync(guardianToken, issued.TokenId, expectedStatus: 204);
        await RevokeAsync(guardianToken, issued.TokenId, expectedStatus: 204);

        Assert.Empty(await PickupTestHelpers.ListIcalTokensAsync(fixture, guardianToken));
    }

    [Fact]
    public async Task A_guardian_cannot_revoke_another_guardians_token()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, ownerToken);

        // The route acts on the caller's own feed, so another guardian's token id is simply unknown.
        await RevokeAsync(otherToken, issued.TokenId, expectedStatus: 204);

        await PickupTestHelpers.ReadFeedAsync(fixture, issued.SubscriptionPath);
    }

    [Fact]
    public async Task A_child_account_cannot_revoke_a_token()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await RevokeAsync(childToken, Guid.CreateVersion7(), expectedStatus: 403);
    }

    private Task<IScenarioResult> RevokeAsync(string token, Guid tokenId, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/pickups/me/ical-tokens/{tokenId}");
            _.StatusCodeShouldBe(expectedStatus);
        });
}
