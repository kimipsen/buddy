using Alba;

using buddy.Common;
using buddy.Features.Progress;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Progress;

// Shared setup for the reward redemption tests (docs/backend/analysis/reward-redemption.md): a
// guardian with a linked child, a calendar to earn stars on, and helpers for every reward route.
public sealed record RewardFamily(Guid CalendarId, Guid ChildId, string GuardianToken, string ChildToken);

public static class RewardTestHelpers
{
    public static async Task<RewardFamily> CreateFamilyAsync(BuddyApiFixture fixture)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{child.Id}");
            _.StatusCodeShouldBe(204);
        });

        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        return new RewardFamily(calendarId, child.Id, guardianToken, childToken);
    }

    // Completes `count` one-off tasks assigned to the child, earning one star each. Returns the
    // task ids so a test can un-complete one to revoke its star.
    public static async Task<IReadOnlyList<Guid>> EarnStarsAsync(BuddyApiFixture fixture, RewardFamily family, int count)
    {
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        List<Guid> taskIds = [];

        for (var i = 0; i < count; i++)
        {
            var task = await CalendarTestHelpers.CreateTaskAsync(fixture, family.GuardianToken, family.CalendarId, dueDate: dueDate, assignedTo: family.ChildId);
            Assert.NotNull(task);
            await SetCompletionAsync(fixture, family, task.Id, true);
            taskIds.Add(task.Id);
        }

        return taskIds;
    }

    public static Task SetCompletionAsync(BuddyApiFixture fixture, RewardFamily family, Guid taskId, bool isCompleted) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Patch.Json(new { Date = DateOnly.FromDateTime(DateTime.UtcNow), IsCompleted = isCompleted })
                .ToUrl($"/calendars/{family.CalendarId}/items/{taskId}/completion");
            _.StatusCodeShouldBeOk();
        });

    public static Task<IScenarioResult> PutRewardsAsync(BuddyApiFixture fixture, string token, Guid childId, object[] rewards, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Rewards = rewards }).ToUrl($"/progress/children/{childId}/rewards");
            _.StatusCodeShouldBe(expectedStatus);
        });

    // Configures one reward and returns its id.
    public static async Task<Guid> AddRewardAsync(BuddyApiFixture fixture, RewardFamily family, string name, int cost)
    {
        var response = await PutRewardsAsync(fixture, family.GuardianToken, family.ChildId, [new { Name = name, Icon = "🎁", Cost = cost }]);

        return response.ReadAsJson<ProgressSummary>().Rewards.Single().Id;
    }

    public static Task<IScenarioResult> RequestRewardAsync(BuddyApiFixture fixture, string childToken, Guid rewardId, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { RewardId = rewardId }).ToUrl("/progress/me/reward-requests");
            _.StatusCodeShouldBe(expectedStatus);
        });

    // Requests a reward and returns the new pending request's id.
    public static async Task<Guid> RequestAsync(BuddyApiFixture fixture, RewardFamily family, Guid rewardId)
    {
        var response = await RequestRewardAsync(fixture, family.ChildToken, rewardId);

        return response.ReadAsJson<ProgressSummary>().RewardRequests
            .Where(r => r.Status == RewardRequestStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .First().Id;
    }

    public static Task<IScenarioResult> ResolveAsync(BuddyApiFixture fixture, string token, Guid childId, Guid requestId, string action, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url($"/progress/children/{childId}/reward-requests/{requestId}/{action}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static Task<IScenarioResult> CancelAsync(BuddyApiFixture fixture, string childToken, Guid requestId, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Url($"/progress/me/reward-requests/{requestId}/cancel");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<ProgressSummary> GetMyProgressAsync(BuddyApiFixture fixture, string childToken)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Get.Url("/progress/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<ProgressSummary>();
    }

    public static void AssertErrorCode(IScenarioResult response, string code) =>
        Assert.Equal(code, response.ReadAsJson<ErrorEnvelope>().Code);
}
