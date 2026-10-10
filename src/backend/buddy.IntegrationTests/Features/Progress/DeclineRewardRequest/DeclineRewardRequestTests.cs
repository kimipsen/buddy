using buddy.Features.Progress;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.Progress.RewardTestHelpers;

namespace buddy.IntegrationTests.Features.Progress.DeclineRewardRequest;

[Collection(BuddyApiCollection.Name)]
public sealed class DeclineRewardRequestTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeclineRewardRequest")]
    public async Task Declining_releases_the_reserved_stars()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 3);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));

        var response = await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "decline");

        var summary = response.ReadAsJson<ProgressSummary>();
        Assert.Equal(0, summary.SpentStars);
        Assert.Equal(3, summary.SpendableStars);
        Assert.Equal(RewardRequestStatus.Declined, summary.RewardRequests.Single().Status);
    }

    [Fact]
    public async Task Declining_twice_is_idempotent()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "decline");

        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "decline");
    }

    [Fact]
    public async Task An_approved_request_cannot_be_declined()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve");

        var response = await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "decline", expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.AlreadyResolvedCode);
    }

    [Fact]
    public async Task The_child_cannot_decline_a_request()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));

        await ResolveAsync(fixture, family.ChildToken, family.ChildId, requestId, "decline", expectedStatus: 403);
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await ResolveAsync(fixture, strangerToken, family.ChildId, requestId, "decline", expectedStatus: 404);
    }
}
