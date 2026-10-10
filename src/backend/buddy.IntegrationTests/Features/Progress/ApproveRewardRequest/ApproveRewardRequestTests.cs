using buddy.Features.Progress;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.Progress.RewardTestHelpers;

namespace buddy.IntegrationTests.Features.Progress.ApproveRewardRequest;

[Collection(BuddyApiCollection.Name)]
public sealed class ApproveRewardRequestTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ApproveRewardRequest")]
    public async Task Approving_spends_the_stars_but_keeps_the_lifetime_total()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 5);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 2);
        var requestId = await RequestAsync(fixture, family, rewardId);

        var response = await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve");

        var summary = response.ReadAsJson<ProgressSummary>();
        Assert.Equal(5, summary.TotalStars);
        Assert.Equal(2, summary.SpentStars);
        Assert.Equal(3, summary.SpendableStars);
        var request = Assert.Single(summary.RewardRequests);
        Assert.Equal(RewardRequestStatus.Approved, request.Status);
        Assert.NotNull(request.ResolvedAt);

        // The child sees the same outcome.
        var childView = await GetMyProgressAsync(fixture, family.ChildToken);
        Assert.Equal(3, childView.SpendableStars);
        Assert.Equal(RewardRequestStatus.Approved, childView.RewardRequests.Single().Status);
    }

    [Fact]
    public async Task Approving_twice_is_idempotent()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 5);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve");

        var response = await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve");

        Assert.Equal(2, response.ReadAsJson<ProgressSummary>().SpentStars);
    }

    [Fact]
    public async Task A_declined_request_cannot_be_approved()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 5);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "decline");

        var response = await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve", expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.AlreadyResolvedCode);
    }

    [Fact]
    public async Task A_cancelled_request_cannot_be_approved()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 5);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await CancelAsync(fixture, family.ChildToken, requestId);

        var response = await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve", expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.AlreadyResolvedCode);
    }

    [Fact]
    public async Task Approval_is_refused_when_a_revoked_star_left_too_few()
    {
        var family = await CreateFamilyAsync(fixture);
        var taskIds = await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        await SetCompletionAsync(fixture, family, taskIds[0], false);

        var response = await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "approve", expectedStatus: 409);

        AssertErrorCode(response, RewardRequestOutcome.InsufficientStarsCode);
        // Declining still works.
        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "decline");
    }

    [Fact]
    public async Task The_child_cannot_approve_their_own_request()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));

        await ResolveAsync(fixture, family.ChildToken, family.ChildId, requestId, "approve", expectedStatus: 403);
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 2);
        var requestId = await RequestAsync(fixture, family, await AddRewardAsync(fixture, family, "Screen time", 2));
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await ResolveAsync(fixture, strangerToken, family.ChildId, requestId, "approve", expectedStatus: 404);
    }

    [Fact]
    public async Task An_unknown_request_is_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        await AddRewardAsync(fixture, family, "Screen time", 2);

        await ResolveAsync(fixture, family.GuardianToken, family.ChildId, Guid.NewGuid(), "approve", expectedStatus: 404);
    }
}
