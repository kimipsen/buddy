using Alba;

using buddy.Features.Progress;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Progress.ConfigureGoalPosts;

[Collection(BuddyApiCollection.Name)]
public sealed class ConfigureGoalPostsTests(BuddyApiFixture fixture)
{
    private async Task<(Guid CalendarId, Guid ChildId, string GuardianToken, string ChildToken)> CreateChildWithCalendarAsync()
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

        return (calendarId, child.Id, guardianToken, childToken);
    }

    private async Task CompleteOneOffTaskAsync(Guid calendarId, string guardianToken, Guid childId, DateOnly dueDate)
    {
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, dueDate: dueDate, assignedTo: childId);
        Assert.NotNull(task);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    [CoversEndpoint("ConfigureGoalPosts")]
    public async Task Guardian_can_configure_goal_posts_and_they_are_reflected_in_progress_summary()
    {
        var (_, childId, guardianToken, _) = await CreateChildWithCalendarAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { GoalPosts = new[] { new { Threshold = 1, Icon = "🥉", Label = (string?)null }, new { Threshold = 2, Icon = "🥈", Label = (string?)null } } })
                .ToUrl($"/progress/children/{childId}/goals");
            _.StatusCodeShouldBeOk();
        });

        var summary = response.ReadAsJson<ProgressSummary>();
        Assert.Equal(2, summary.GoalPosts.Count);
        Assert.Equal(1, summary.GoalPosts[0].Threshold);
        Assert.Equal("🥉", summary.GoalPosts[0].Icon);
        Assert.Equal(1, summary.NextGoalThreshold);
    }

    [Fact]
    public async Task Child_cannot_configure_their_own_goal_posts()
    {
        var (_, childId, guardianToken, childToken) = await CreateChildWithCalendarAsync();
        _ = guardianToken;

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { GoalPosts = new[] { new { Threshold = 1, Icon = "🥉", Label = (string?)null } } })
                .ToUrl($"/progress/children/{childId}/goals");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Empty_goal_post_list_is_rejected()
    {
        var (_, childId, guardianToken, _) = await CreateChildWithCalendarAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { GoalPosts = Array.Empty<object>() })
                .ToUrl($"/progress/children/{childId}/goals");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Progress_keeps_generating_goal_posts_past_the_configured_list()
    {
        var (calendarId, childId, guardianToken, _) = await CreateChildWithCalendarAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { GoalPosts = new[] { new { Threshold = 1, Icon = "🥉", Label = (string?)null }, new { Threshold = 2, Icon = "🥈", Label = (string?)null } } })
                .ToUrl($"/progress/children/{childId}/goals");
            _.StatusCodeShouldBeOk();
        });

        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Four distinct one-off tasks due the same day -- each is its own awardable occurrence
        // (keyed by ItemId, not just date), taking the child past both configured thresholds
        // (1, 2) into the extrapolated range (step = 1, so 3 and 4 keep unlocking with cycled
        // icons).
        for (var i = 0; i < 4; i++)
        {
            await CompleteOneOffTaskAsync(calendarId, guardianToken, childId, dueDate);
        }

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/progress/children/{childId}");
            _.StatusCodeShouldBeOk();
        });

        var summary = response.ReadAsJson<ProgressSummary>();
        Assert.Equal(4, summary.TotalStars);
        Assert.Equal([1, 2, 3, 4], summary.UnlockedMilestones);
        Assert.Equal("🥈", summary.CurrentIcon);
        Assert.Equal(5, summary.NextGoalThreshold);
        Assert.Equal("🥉", summary.NextGoalIcon);
    }
}
