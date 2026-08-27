using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.SetTaskCompletion;

[Collection(BuddyApiCollection.Name)]
public sealed class SetTaskCompletionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetTaskCompletion")]
    public async Task A_contributor_can_mark_a_task_complete()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, dueDate: dueDate);
        Assert.NotNull(task);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Date = task.DueDate!.Date, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<TaskCompletionResponseDto>();
        Assert.True(updated.IsCompleted);

        var occurrences = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={task.DueDate!.Date:yyyy-MM-dd}&to={task.DueDate.Date:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrence = Assert.Single(occurrences.ReadAsJson<List<CalendarItemOccurrenceDto>>());
        Assert.True(occurrence.IsCompleted);
    }

    [Fact]
    public async Task Completing_one_occurrence_of_a_recurring_task_does_not_affect_another()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var firstDue = DateOnly.FromDateTime(DateTime.UtcNow);
        var task = await CalendarTestHelpers.CreateTaskAsync(
            fixture, token, calendarId, dueDate: firstDue,
            recurrence: new RecurrenceRuleRequest(RecurrenceFrequency.Daily, 1, null));
        Assert.NotNull(task);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Date = firstDue, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        var secondDue = firstDue.AddDays(1);
        var occurrences = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={firstDue:yyyy-MM-dd}&to={secondDue:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var byDate = occurrences.ReadAsJson<List<CalendarItemOccurrenceDto>>()
            .ToDictionary(o => DateOnly.FromDateTime(o.DueAt!.Value.Date));

        Assert.True(byDate[firstDue].IsCompleted);
        Assert.False(byDate[secondDue].IsCompleted);
    }

    [Fact]
    public async Task Marking_a_future_occurrence_complete_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var futureDue = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var task = await CalendarTestHelpers.CreateTaskAsync(
            fixture, token, calendarId, dueDate: futureDue,
            recurrence: new RecurrenceRuleRequest(RecurrenceFrequency.Daily, 1, null));
        Assert.NotNull(task);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Date = futureDue, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Marking_an_event_complete_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Date = DateOnly.FromDateTime(DateTime.UtcNow), IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{item.Id}/completion");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_stranger_with_no_calendar_access_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Personal");
        var task = await CalendarTestHelpers.CreateTaskAsync(fixture, ownerToken, calendarId);
        Assert.NotNull(task);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Patch.Json(new { Date = task.DueDate!.Date, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task A_child_with_only_viewer_access_can_complete_their_own_assigned_task()
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
        var task = await CalendarTestHelpers.CreateTaskAsync(
            fixture, guardianToken, calendarId, dueDate: DateOnly.FromDateTime(DateTime.UtcNow), assignedTo: child.Id);
        Assert.NotNull(task);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Patch.Json(new { Date = task.DueDate!.Date, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{task.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        Assert.True(response.ReadAsJson<TaskCompletionResponseDto>().IsCompleted);
    }

    [Fact]
    public async Task A_child_with_only_viewer_access_cannot_complete_an_unassigned_or_sibling_task()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);

        var childA = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childB = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Sam");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{childA.Id}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{childB.Id}");
            _.StatusCodeShouldBe(204);
        });

        var childAToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, childA);
        var unassignedTask = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId);
        var siblingTask = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, assignedTo: childB.Id);
        Assert.NotNull(unassignedTask);
        Assert.NotNull(siblingTask);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childAToken}");
            _.Patch.Json(new { Date = unassignedTask.DueDate!.Date, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{unassignedTask.Id}/completion");
            _.StatusCodeShouldBe(403);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childAToken}");
            _.Patch.Json(new { Date = siblingTask.DueDate!.Date, IsCompleted = true })
                .ToUrl($"/calendars/{calendarId}/items/{siblingTask.Id}/completion");
            _.StatusCodeShouldBe(403);
        });
    }
}
