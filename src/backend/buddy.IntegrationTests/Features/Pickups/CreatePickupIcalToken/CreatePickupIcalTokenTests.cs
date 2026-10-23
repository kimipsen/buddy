using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Pickups.CreatePickupIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class CreatePickupIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreatePickupIcalToken")]
    public async Task A_guardian_can_issue_a_subscription_token_for_their_own_feed()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);

        Assert.NotEmpty(issued.Token);
        Assert.Equal($"/pickups/feeds/{guardianId}/ical/{issued.Token}", issued.SubscriptionPath);
    }

    [Fact]
    public async Task A_guardian_with_no_children_gets_a_working_empty_feed()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var issued = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);
        var ics = await PickupTestHelpers.ReadFeedAsync(fixture, issued.SubscriptionPath);

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.DoesNotContain("BEGIN:VEVENT", ics);
    }

    [Fact]
    public async Task A_second_token_leaves_the_first_one_working()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var first = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);
        var second = await PickupTestHelpers.CreateIcalTokenAsync(fixture, guardianToken);

        Assert.NotEqual(first.TokenId, second.TokenId);
        await PickupTestHelpers.ReadFeedAsync(fixture, first.SubscriptionPath);
        await PickupTestHelpers.ReadFeedAsync(fixture, second.SubscriptionPath);
        Assert.Equal(2, (await PickupTestHelpers.ListIcalTokensAsync(fixture, guardianToken)).Count);
    }

    [Fact]
    public async Task A_child_account_cannot_issue_a_subscription_token()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Url("/pickups/me/ical-tokens");
            _.StatusCodeShouldBe(403);
        });
    }
}
