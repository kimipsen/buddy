using Alba;

using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.ListPickupIcalTokens;

[Collection(BuddyApiCollection.Name)]
public sealed class ListPickupIcalTokensTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListPickupIcalTokens")]
    public async Task A_guardian_sees_their_tokens_newest_first_without_the_secret()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var first = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);
        var second = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/pickups/me/ical-tokens");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsText();
        var tokens = response.ReadAsJson<List<PickupIcalTokenSummaryDto>>();

        Assert.Equal([second.TokenId, first.TokenId], tokens.Select(t => t.TokenId));
        Assert.DoesNotContain(first.Token, body);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_guardian_who_never_subscribed_gets_an_empty_list()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        Assert.Empty(await PickupTestHelpers.ListIcalTokensAsync(fixture, guardianToken));
    }

    [Fact]
    public async Task Another_guardians_tokens_are_not_listed()
    {
        var (_, firstToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, secondToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await PickupTestHelpers.CreateIcalTokenAsync(fixture, firstToken);

        Assert.Empty(await PickupTestHelpers.ListIcalTokensAsync(fixture, secondToken));
    }

    [Fact]
    public async Task A_child_account_cannot_list_tokens()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url("/pickups/me/ical-tokens");
            _.StatusCodeShouldBe(403);
        });
    }
}
