using Alba;

using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.ListOccurrences;

[Collection(BuddyApiCollection.Name)]
public sealed class ChildTaskVisibilityTests(BuddyApiFixture fixture)
{
    private static async Task AddChildToGroupAsync(BuddyApiFixture fixture, string guardianToken, Guid groupId, Guid childId)
    {
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{childId}");
            _.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task A_child_sees_only_their_own_assigned_tasks_but_still_sees_events()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);

        var childA = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childB = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Sam");
        await AddChildToGroupAsync(fixture, guardianToken, groupId, childA.Id);
        await AddChildToGroupAsync(fixture, guardianToken, groupId, childB.Id);
        var childAToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, childA);

        var due = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, "Alex's chore", due, assignedTo: childA.Id);
        await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, "Sam's chore", due, assignedTo: childB.Id);
        await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, "Unassigned chore", due);
        await CalendarTestHelpers.CreateEventAsync(fixture, guardianToken, calendarId, "Family dinner", due);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childAToken}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={due:yyyy-MM-dd}&to={due:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrences = response.ReadAsJson<CalendarItemOccurrenceDto[]>();
        Assert.Contains(occurrences, o => o.Title == "Alex's chore");
        Assert.Contains(occurrences, o => o.Title == "Family dinner");
        Assert.DoesNotContain(occurrences, o => o.Title == "Sam's chore");
        Assert.DoesNotContain(occurrences, o => o.Title == "Unassigned chore");
    }

    [Fact]
    public async Task A_guardian_sees_every_task_regardless_of_assignment_along_with_its_status()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);

        var childA = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AddChildToGroupAsync(fixture, guardianToken, groupId, childA.Id);

        var due = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var assigned = await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, "Alex's chore", due, assignedTo: childA.Id);
        await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, calendarId, "Unassigned chore", due);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Date = due, IsCompleted = true }).ToUrl($"/calendars/{calendarId}/items/{assigned!.Id}/completion");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={due:yyyy-MM-dd}&to={due:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrences = response.ReadAsJson<CalendarItemOccurrenceDto[]>();
        Assert.Contains(occurrences, o => o.Title == "Unassigned chore");
        var alexChore = Assert.Single(occurrences, o => o.Title == "Alex's chore");
        Assert.True(alexChore.IsCompleted);
    }
}
