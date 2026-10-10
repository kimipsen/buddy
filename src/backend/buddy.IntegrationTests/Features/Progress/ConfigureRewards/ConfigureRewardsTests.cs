using Alba;

using buddy.Common;
using buddy.Features.Progress;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.Progress.RewardTestHelpers;

namespace buddy.IntegrationTests.Features.Progress.ConfigureRewards;

[Collection(BuddyApiCollection.Name)]
public sealed class ConfigureRewardsTests(BuddyApiFixture fixture)
{
    private static void AssertValidationErrorOn(IScenarioResult response, string field)
    {
        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains(field, error.Details.Keys);
    }

    [Fact]
    [CoversEndpoint("ConfigureRewards")]
    public async Task A_guardian_can_configure_a_childs_rewards_and_the_child_sees_them()
    {
        var family = await CreateFamilyAsync(fixture);

        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId,
        [
            new { Name = "Extra screen time", Icon = "📱", Cost = 5 },
            new { Name = "Choose dinner", Icon = "🍕", Cost = 12 }
        ]);

        var summary = response.ReadAsJson<ProgressSummary>();
        Assert.Equal(["Extra screen time", "Choose dinner"], summary.Rewards.Select(r => r.Name));
        Assert.Equal([5, 12], summary.Rewards.Select(r => r.Cost));
        Assert.All(summary.Rewards, r => Assert.NotEqual(Guid.Empty, r.Id));

        var childView = await GetMyProgressAsync(fixture, family.ChildToken);
        Assert.Equal(summary.Rewards, childView.Rewards);
    }

    [Fact]
    public async Task A_reward_keeps_its_id_when_the_guardian_edits_it()
    {
        var family = await CreateFamilyAsync(fixture);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 5);

        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId,
        [
            new { Id = rewardId, Name = "20 minutes screen time", Icon = "📱", Cost = 7 },
            new { Name = "Choose dinner", Icon = "🍕", Cost = 12 }
        ]);

        var rewards = response.ReadAsJson<ProgressSummary>().Rewards;
        Assert.Equal(rewardId, rewards[0].Id);
        Assert.Equal("20 minutes screen time", rewards[0].Name);
        Assert.Equal(7, rewards[0].Cost);
        Assert.NotEqual(rewardId, rewards[1].Id);
    }

    [Fact]
    public async Task An_empty_catalog_removes_every_reward()
    {
        var family = await CreateFamilyAsync(fixture);
        await AddRewardAsync(fixture, family, "Extra screen time", 5);

        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId, []);

        Assert.Empty(response.ReadAsJson<ProgressSummary>().Rewards);
    }

    [Fact]
    public async Task An_id_that_is_not_in_the_catalog_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);

        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId,
            [new { Id = Guid.NewGuid(), Name = "Made up", Icon = "🎁", Cost = 5 }], expectedStatus: 400);

        AssertValidationErrorOn(response, "Rewards[0].Id");
    }

    [Fact]
    public async Task The_same_id_twice_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        var rewardId = await AddRewardAsync(fixture, family, "Extra screen time", 5);

        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId,
        [
            new { Id = rewardId, Name = "A", Icon = "🎁", Cost = 5 },
            new { Id = rewardId, Name = "B", Icon = "🎁", Cost = 6 }
        ], expectedStatus: 400);

        AssertValidationErrorOn(response, "Rewards");
    }

    [Theory]
    [InlineData("", "🎁", 5, "Rewards[0].Name")]
    [InlineData("Screen time", "", 5, "Rewards[0].Icon")]
    [InlineData("Screen time", "🎁", 0, "Rewards[0].Cost")]
    [InlineData("Screen time", "🎁", 10_001, "Rewards[0].Cost")]
    public async Task An_invalid_reward_is_rejected(string name, string icon, int cost, string field)
    {
        var family = await CreateFamilyAsync(fixture);

        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId,
            [new { Name = name, Icon = icon, Cost = cost }], expectedStatus: 400);

        AssertValidationErrorOn(response, field);
    }

    [Fact]
    public async Task A_name_over_100_characters_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);

        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId,
            [new { Name = new string('a', 101), Icon = "🎁", Cost = 5 }], expectedStatus: 400);

        AssertValidationErrorOn(response, "Rewards[0].Name");
    }

    [Fact]
    public async Task Thirty_rewards_are_accepted_and_thirty_one_are_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        static object[] Rewards(int count) => [.. Enumerable.Range(1, count).Select(i => new { Name = $"Reward {i}", Icon = "🎁", Cost = i })];

        await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId, Rewards(30));
        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId, Rewards(31), expectedStatus: 400);

        AssertValidationErrorOn(response, "Rewards");
    }

    [Fact]
    public async Task A_null_reward_in_the_list_is_rejected_rather_than_failing()
    {
        var family = await CreateFamilyAsync(fixture);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Put.Json(new { Rewards = new object?[] { null } }).ToUrl($"/progress/children/{family.ChildId}/rewards");
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task The_child_cannot_configure_their_own_rewards()
    {
        var family = await CreateFamilyAsync(fixture);

        await PutRewardsAsync(fixture, family.ChildToken, family.ChildId,
            [new { Name = "Everything", Icon = "🎁", Cost = 1 }], expectedStatus: 403);
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await PutRewardsAsync(fixture, strangerToken, family.ChildId,
            [new { Name = "Screen time", Icon = "🎁", Cost = 1 }], expectedStatus: 404);
    }
}
