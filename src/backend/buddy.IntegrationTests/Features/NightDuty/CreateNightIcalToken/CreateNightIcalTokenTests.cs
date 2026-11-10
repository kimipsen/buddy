using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.NightDuty.CreateNightIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateNightIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateNightIcalToken")]
    public async Task A_guardian_can_issue_a_subscription_token_for_a_childs_nights()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var issued = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        Assert.NotEmpty(issued.Token);
        Assert.Equal($"/night-duty/feeds/{guardianId}/ical/{issued.Token}", issued.SubscriptionPath);
        var listed = Assert.Single(await NightDutyTestHelpers.ListIcalTokensAsync(fixture, guardianToken, child.Id));
        Assert.Equal(issued.TokenId, listed.TokenId);
    }

    [Fact]
    public async Task A_child_with_no_nights_gets_a_working_empty_feed()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var issued = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);
        var ics = await NightDutyTestHelpers.ReadFeedAsync(fixture, issued.SubscriptionPath);

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("X-WR-CALNAME:Night duty: Alex", ics);
        Assert.DoesNotContain("BEGIN:VEVENT", ics);
    }

    [Fact]
    public async Task A_second_token_leaves_the_first_one_working()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var first = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);
        var second = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        Assert.NotEqual(first.TokenId, second.TokenId);
        await NightDutyTestHelpers.ReadFeedAsync(fixture, first.SubscriptionPath);
        await NightDutyTestHelpers.ReadFeedAsync(fixture, second.SubscriptionPath);
        Assert.Equal(2, (await NightDutyTestHelpers.ListIcalTokensAsync(fixture, guardianToken, child.Id)).Count);
    }

    [Fact]
    public async Task The_child_themself_is_forbidden()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Url($"/night-duty/children/{child.Id}/ical-tokens");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Someone_who_is_not_a_guardian_of_the_child_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Post.Url($"/night-duty/children/{child.Id}/ical-tokens");
            _.StatusCodeShouldBe(404);
        });
    }
}
