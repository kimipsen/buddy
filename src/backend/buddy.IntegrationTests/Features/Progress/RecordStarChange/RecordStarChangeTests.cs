using Alba;

using buddy.Features.Calendars;
using buddy.Features.Progress;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Progress.RecordStarChange;

// RecordStarChange has no HTTP endpoint of its own (see ProgressFeature) -- it's only ever
// invoked from SetTaskCompletionHandler, so these tests drive it indirectly through the
// completion endpoint and assert on the resulting star count via GetChildProgress/GetMyProgress.
[Collection(BuddyApiCollection.Name)]
public sealed class RecordStarChangeTests(BuddyApiFixture fixture)
{
    private async Task<(Guid CalendarId, Guid ChildId, string GuardianToken, Guid ItemId, Guid[] SubtaskIds, DateOnly StartDate)> ScheduleTemplateTaskWithThreeSubtasksAsync()
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

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions("Brush teeth", "toothbrush", "00:10:00"));
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions("Get dressed", "shirt", "00:15:00"));
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id, new AddSubtaskOptions("Pack bag", "bag", "00:05:00"));

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var item = await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(
            fixture, guardianToken, calendarId, template.Id, "Morning routine", startDate, new TimeOnly(7, 0), assignedTo: child.Id);
        Assert.NotNull(item);

        var occurrencesResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={startDate:yyyy-MM-dd}&to={startDate:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrences = occurrencesResponse.ReadAsJson<CalendarItemOccurrenceDto[]>().OrderBy(o => o.StartsAt).ToArray();
        Assert.Equal(3, occurrences.Length);

        return (calendarId, child.Id, guardianToken, item.Id, [.. occurrences.Select(o => o.SubtaskId!.Value)], startDate);
    }

    private async Task CompleteSubtaskAsync(Guid calendarId, Guid itemId, string guardianToken, DateOnly date, Guid subtaskId, bool isCompleted = true)
    {
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = date, IsCompleted = isCompleted, SubtaskId = subtaskId })
                .ToUrl($"/calendars/{calendarId}/items/{itemId}/completion");
            _.StatusCodeShouldBeOk();
        });
    }

    private async Task<ProgressSummary> GetChildProgressAsync(string callerToken, Guid childId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {callerToken}");
            _.Get.Url($"/progress/children/{childId}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<ProgressSummary>();
    }

    private async Task<ProgressSummary> GetMyProgressAsync(string callerToken)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {callerToken}");
            _.Get.Url("/progress/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<ProgressSummary>();
    }

    [Fact]
    [CoversEndpoint("GetChildProgress")]
    public async Task Completing_three_distinct_subtasks_of_the_same_item_on_the_same_day_awards_three_stars()
    {
        var (calendarId, childId, guardianToken, itemId, subtaskIds, startDate) = await ScheduleTemplateTaskWithThreeSubtasksAsync();

        foreach (var subtaskId in subtaskIds)
        {
            await CompleteSubtaskAsync(calendarId, itemId, guardianToken, startDate, subtaskId);
        }

        var summary = await GetChildProgressAsync(guardianToken, childId);
        Assert.Equal(3, summary.TotalStars);
    }

    [Fact]
    public async Task Uncompleting_one_subtask_revokes_only_its_own_star()
    {
        var (calendarId, childId, guardianToken, itemId, subtaskIds, startDate) = await ScheduleTemplateTaskWithThreeSubtasksAsync();

        foreach (var subtaskId in subtaskIds)
        {
            await CompleteSubtaskAsync(calendarId, itemId, guardianToken, startDate, subtaskId);
        }

        await CompleteSubtaskAsync(calendarId, itemId, guardianToken, startDate, subtaskIds[0], isCompleted: false);

        var summary = await GetChildProgressAsync(guardianToken, childId);
        Assert.Equal(2, summary.TotalStars);
    }

    [Fact]
    public async Task Completing_the_same_subtask_twice_does_not_double_award_a_star()
    {
        var (calendarId, childId, guardianToken, itemId, subtaskIds, startDate) = await ScheduleTemplateTaskWithThreeSubtasksAsync();

        await CompleteSubtaskAsync(calendarId, itemId, guardianToken, startDate, subtaskIds[0]);

        // Re-marking an already-completed subtask complete is a before == after no-op in
        // SetTaskCompletionHandler, so RecordStarChange is never even invoked a second time --
        // this just confirms the star count doesn't move.
        await CompleteSubtaskAsync(calendarId, itemId, guardianToken, startDate, subtaskIds[0]);

        var summary = await GetChildProgressAsync(guardianToken, childId);
        Assert.Equal(1, summary.TotalStars);
    }

    [Fact]
    [CoversEndpoint("GetMyProgress")]
    public async Task A_plain_non_template_tasks_star_award_and_revoke_behavior_is_unchanged()
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
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, dueDate: dueDate, assignedTo: child.Id);
        Assert.NotNull(task);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        var afterCompletion = await GetMyProgressAsync(childToken);
        Assert.Equal(1, afterCompletion.TotalStars);

        // Completing the same (non-template) task a second time is a no-op -- still one star.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        var afterDuplicateCompletion = await GetMyProgressAsync(childToken);
        Assert.Equal(1, afterDuplicateCompletion.TotalStars);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = dueDate, IsCompleted = false })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        var afterRevoke = await GetMyProgressAsync(childToken);
        Assert.Equal(0, afterRevoke.TotalStars);
    }
}
