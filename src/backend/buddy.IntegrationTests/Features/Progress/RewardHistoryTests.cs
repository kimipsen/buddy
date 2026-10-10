using System.Text;
using System.Text.Json;

using Alba;

using buddy.Features.Progress;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Progress.RewardTestHelpers;

namespace buddy.IntegrationTests.Features.Progress;

// The parts of reward redemption that outlive one request: how much history the summary keeps,
// that the personal data export keeps all of it, and that progress snapshots written before
// rewards existed still load (docs/backend/analysis/reward-redemption.md).
[Collection(BuddyApiCollection.Name)]
public sealed class RewardHistoryTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_summary_keeps_the_latest_resolved_requests_and_the_export_keeps_every_one()
    {
        var family = await CreateFamilyAsync(fixture);
        await EarnStarsAsync(fixture, family, 1);
        var rewardId = await AddRewardAsync(fixture, family, "Sticker", 1);
        var total = ProgressSummary.ResolvedRequestHistory + 1;

        for (var i = 0; i < total; i++)
        {
            var requestId = await RequestAsync(fixture, family, rewardId);
            await ResolveAsync(fixture, family.GuardianToken, family.ChildId, requestId, "decline");
        }

        var summary = await GetMyProgressAsync(fixture, family.ChildToken);
        Assert.Equal(ProgressSummary.ResolvedRequestHistory, summary.RewardRequests.Count);
        Assert.Equal(summary.RewardRequests.OrderByDescending(r => r.ResolvedAt), summary.RewardRequests);

        var export = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Get.Url("/users/me/export");
            _.StatusCodeShouldBeOk();
        });

        using var json = JsonDocument.Parse(await export.ReadAsTextAsync());
        var child = Assert.Single(json.RootElement.GetProperty("sections").GetProperty("progress").EnumerateArray());
        Assert.Equal(total, child.GetProperty("allRewardRequests").GetArrayLength());
    }

    // A snapshot stored before reward redemption has no rewards, spentStars or rewardRequests.
    // ChildProgress must read those as empty (not a default ImmutableArray, which throws on use).
    [Fact]
    public void A_snapshot_from_before_rewards_still_loads_and_folds_reward_events()
    {
        const string before = """
            {
              "Id": "00000000-0000-0000-0000-000000000003",
              "ChildProgress": {
                "Id": "00000000-0000-0000-0000-000000000003",
                "ChildId": "00000000-0000-0000-0000-000000000003",
                "TotalStars": 3,
                "AwardedOccurrences": [],
                "UnlockedMilestones": [],
                "GoalPosts": []
              }
            }
            """;

        var serializer = fixture.Host.Services.GetRequiredService<IProgressStore>().Options.Serializer();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(before));
        var progress = serializer.FromJson<ChildProgressSnapshot>(stream).ChildProgress;

        Assert.Empty(progress.Rewards);
        Assert.Empty(progress.RewardRequests);
        Assert.Equal(0, progress.SpentStars);
        Assert.Equal(3, progress.SpendableStars);

        var configured = ChildProgress.Advance(progress, new RewardsConfigured(
            progress.Id, [new Reward(RewardId.New(), "Sticker", "⭐", 1)], progress.ChildId, DateTimeOffset.UtcNow));
        Assert.Single(configured.Rewards);
        Assert.Empty(ProgressSummary.From(configured).RewardRequests);
    }
}
