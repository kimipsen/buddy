using buddy.Features.Progress;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.Progress.RewardTestHelpers;

namespace buddy.IntegrationTests.Features.Progress.CancelRewardRequest;

[Collection(BuddyApiCollection.Name)]
public sealed class CancelRewardRequestTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CancelRewardRequest")]
    public async Task A_child_can_withdraw_a_pending_request()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));

        var response = await CancelAsync(fixture, family.ChildToken, requestId);

        var summary = response.ReadAsJson<ProgressSummary>();
        Assert.Equal(2, summary.SpendableStars);
        Assert.Equal(RewardRequestStatus.Cancelled, summary.RewardRequests.Single().Status);
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await CancelAsync(fixture, family.ChildToken, requestId);

        await CancelAsync(fixture, family.ChildToken, requestId);
    }

    [Fact]
    public async Task An_approved_request_cannot_be_withdrawn()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve");

        var response = await CancelAsync(fixture, family.ChildToken, requestId, expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.AlreadyResolvedCode);
    }

    [Fact]
    public async Task A_child_cannot_withdraw_another_childs_request()
    {
        var family = await CreateFamilyAsync(fixture);
        var other = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, other, 1);
        var otherRequestId = await RequestAsync(fixture, other, await AddRewardAsync(fixture, other, "Screen time", 1));

        await CancelAsync(fixture, family.ChildToken, otherRequestId, expectedStatus: 404);
    }
}
