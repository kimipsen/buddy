using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.NightDuty.RevokeNightIcalToken;

[Collection(BuddyApiCollection.Name)]
public sealed class RevokeNightIcalTokenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RevokeNightIcalToken")]
    public async Task A_revoked_token_stops_working_and_the_other_token_keeps_working()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var revoked = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);
        var kept = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, guardianToken, child.Id, revoked.TokenId);

        await NightDutyTestHelpers.FeedShouldBeNotFoundAsync(fixture, revoked.SubscriptionPath);
        await NightDutyTestHelpers.ReadFeedAsync(fixture, kept.SubscriptionPath);
        var listed = Assert.Single(await NightDutyTestHelpers.ListIcalTokensAsync(fixture, guardianToken, child.Id));
        Assert.Equal(kept.TokenId, listed.TokenId);
    }

    [Fact]
    public async Task Revoking_an_unknown_or_already_revoked_token_is_a_no_op()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var issued = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);

        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, guardianToken, child.Id, Guid.CreateVersion7());
        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, guardianToken, child.Id, issued.TokenId);
        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, guardianToken, child.Id, issued.TokenId);

        Assert.Empty(await NightDutyTestHelpers.ListIcalTokensAsync(fixture, guardianToken, child.Id));
    }

    [Fact]
    public async Task A_token_is_only_revoked_through_the_child_it_was_issued_for()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var bea = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Bea");
        var issued = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, alex.Id);

        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, guardianToken, bea.Id, issued.TokenId);

        await NightDutyTestHelpers.ReadFeedAsync(fixture, issued.SubscriptionPath);
    }

    [Fact]
    public async Task The_child_is_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var issued = await NightDutyTestHelpers.CreateIcalTokenAsync(fixture, guardianToken, child.Id);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, childToken, child.Id, issued.TokenId, 403);
        await NightDutyTestHelpers.RevokeIcalTokenAsync(fixture, strangerToken, child.Id, issued.TokenId, 404);

        await NightDutyTestHelpers.ReadFeedAsync(fixture, issued.SubscriptionPath);
    }
}
