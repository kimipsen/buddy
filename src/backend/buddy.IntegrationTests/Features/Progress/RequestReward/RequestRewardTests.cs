using buddy.Features.Progress;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.Progress.RewardTestHelpers;

namespace buddy.IntegrationTests.Features.Progress.RequestReward;

[Collection(BuddyApiCollection.Name)]
public sealed class RequestRewardTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RequestReward")]
    public async Task A_child_can_request_an_affordable_reward_which_reserves_its_stars()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 3);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 2);

        var response = await RequestRewardAsync(fixture, family.ChildToken, rewardId);

        var summary = response.ReadAsJson<ProgressSummary>();
        var request = Assert.Single(summary.RewardRequests);
        Assert.Equal(RewardRequestStatus.Pending, request.Status);
        Assert.Equal(rewardId, request.RewardId);
        Assert.Equal("Extra screen time", request.Name);
        Assert.Equal(2, request.Cost);
        Assert.Null(request.ResolvedAt);
        // Reserved, not spent: lifetime stars are untouched.
        Assert.Equal(3, summary.TotalStars);
        Assert.Equal(0, summary.SpentStars);
        Assert.Equal(1, summary.SpendableStars);
    }

    [Fact]
    public async Task A_reward_costing_more_than_the_balance_is_refused()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 1);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 2);

        var response = await RequestRewardAsync(fixture, family.ChildToken, rewardId, expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.InsufficientStarsCode);
        Assert.Empty((await GetMyProgressAsync(fixture, family.ChildToken)).RewardRequests);
    }

    [Fact]
    public async Task A_reward_costing_exactly_the_balance_is_allowed()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 2);

        var response = await RequestRewardAsync(fixture, family.ChildToken, rewardId);

        Assert.Equal(0, response.ReadAsJson<ProgressSummary>().SpendableStars);
    }

    [Fact]
    public async Task Pending_requests_together_cannot_exceed_the_balance()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 3);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 2);
        await RequestRewardAsync(fixture, family.ChildToken, rewardId);

        var response = await RequestRewardAsync(fixture, family.ChildToken, rewardId, expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.InsufficientStarsCode);
    }

    [Fact]
    public async Task A_child_can_have_at_most_ten_pending_requests()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, RewardRequestRules.MaxPendingRequests + 1);
        var rewardId = await AddRewardAsync(fixture, family, "Sticker", 1);

        for (var i = 0; i < RewardRequestRules.MaxPendingRequests; i++)
        {
            await RequestRewardAsync(fixture, family.ChildToken, rewardId);
        }

        var response = await RequestRewardAsync(fixture, family.ChildToken, rewardId, expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.TooManyPendingRequestsCode);
    }

    [Fact]
    public async Task A_reward_that_is_not_in_the_catalog_is_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 1);
        await AddRewardAsync(fixture, family, "Extra screen time", 1);

        await RequestRewardAsync(fixture, family.ChildToken, Guid.NewGuid(), expectedStatus: 404);
    }

    [Fact]
    public async Task A_child_without_any_progress_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);

        await RequestRewardAsync(fixture, family.ChildToken, Guid.NewGuid(), expectedStatus: 404);
    }

    [Fact]
    public async Task A_child_cannot_request_another_childs_reward()
    {
        var family = await CreateFamilyAsync(fixture);
        var other = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 1);
        var otherRewardId = await AddRewardAsync(fixture, other, "Their reward", 1);
        await AddRewardAsync(fixture, family, "My reward", 1);

        await RequestRewardAsync(fixture, family.ChildToken, otherRewardId, expectedStatus: 404);
    }

    [Fact]
    public async Task A_pending_request_keeps_the_name_and_cost_the_child_asked_for()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 3);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 2);
        await RequestRewardAsync(fixture, family.ChildToken, rewardId);

        await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId,
            [new { Id = rewardId, Name = "Renamed", Icon = "🎁", Cost = 9 }]);

        var request = Assert.Single((await GetMyProgressAsync(fixture, family.ChildToken)).RewardRequests);
        Assert.Equal("Extra screen time", request.Name);
        Assert.Equal(2, request.Cost);
    }
}
