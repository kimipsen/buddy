using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.NightDuty.ListNightIcalTokens;

[Collection(BuddyApiCollection.Name)]
public sealed class ListNightIcalTokensTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListNightIcalTokens")]
    public async Task A_guardian_sees_only_their_own_tokens_for_that_child_newest_first()
    {
        var coGuardians = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, coGuardians.FirstToken, "Sam");

        var older = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, coGuardians.FirstToken, coGuardians.Child.Id);
        var newer = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, coGuardians.FirstToken, coGuardians.Child.Id);
        await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, coGuardians.FirstToken, sibling.Id);
        await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, coGuardians.SecondToken, coGuardians.Child.Id);

        var tokens = await NightDutyTestHelpers.ListIcalTokensAsync(fixture, coGuardians.FirstToken, coGuardians.Child.Id);

        Assert.Equal([newer.TokenId, older.TokenId], tokens.Select(t => t.TokenId));
    }

    [Fact]
    public async Task A_guardian_who_never_subscribed_gets_an_empty_list()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        Assert.Empty(await NightDutyTestHelpers.ListIcalTokensAsync(fixture, guardianToken, child.Id));
    }

    [Fact]
    public async Task The_child_is_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url($"/night-duty/children/{child.Id}/ical-tokens");
            _.StatusCodeShouldBe(403);
        });
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Get.Url($"/night-duty/children/{child.Id}/ical-tokens");
            _.StatusCodeShouldBe(404);
        });
    }
}
