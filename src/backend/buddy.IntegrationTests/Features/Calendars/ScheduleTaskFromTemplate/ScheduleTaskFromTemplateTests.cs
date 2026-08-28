using Alba;

using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.ScheduleTaskFromTemplate;

[Collection(BuddyApiCollection.Name)]
public sealed class ScheduleTaskFromTemplateTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ScheduleTaskFromTemplate")]
    public async Task A_guardian_can_schedule_a_task_from_a_template_and_its_subtasks_expand_back_to_back()
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

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var startTime = new TimeOnly(7, 0);

        var item = await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(
            fixture, guardianToken, calendarId, template.Id, "Morning routine", startDate, startTime, assignedTo: child.Id);

        Assert.NotNull(item);
        Assert.Equal("Morning routine", item.Title);
        Assert.Equal(startDate, item.DueDate!.Date);
        Assert.Equal(startTime, item.DueDate.Time);
        Assert.Equal(child.Id, item.AssignedTo);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={startDate:yyyy-MM-dd}&to={startDate:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrences = response.ReadAsJson<CalendarItemOccurrenceDto[]>()
            .OrderBy(o => o.StartsAt)
            .ToArray();

        Assert.Equal(2, occurrences.Length);

        var first = occurrences[0];
        var second = occurrences[1];

        Assert.Equal("Brush teeth", first.Title);
        Assert.Equal("Morning routine", first.ParentTitle);
        Assert.NotNull(first.SubtaskId);
        Assert.Equal(TimeSpan.FromMinutes(10), first.EndsAt!.Value - first.StartsAt!.Value);

        Assert.Equal("Get dressed", second.Title);
        Assert.Equal("Morning routine", second.ParentTitle);
        Assert.NotNull(second.SubtaskId);
        Assert.NotEqual(first.SubtaskId, second.SubtaskId);
        Assert.Equal(TimeSpan.FromMinutes(15), second.EndsAt!.Value - second.StartsAt!.Value);

        // Back-to-back: the second subtask starts exactly when the first ends.
        Assert.Equal(first.EndsAt, second.StartsAt);

        // DueAt is populated the same way a plain task's is, so existing overdue-filtering/
        // frontend logic keyed off it keeps working without special-casing.
        Assert.Equal(first.StartsAt, first.DueAt);
        Assert.Equal(second.StartsAt, second.DueAt);
    }

    [Fact]
    public async Task Scheduling_from_an_archived_template_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family");

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, template.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/task-templates/{template.Id}");
            _.StatusCodeShouldBe(204);
        });

        await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(fixture, guardianToken, calendarId, template.Id, assignedTo: child.Id, expectedStatus: 400);
    }

    [Fact]
    public async Task Scheduling_from_a_template_with_no_subtasks_is_rejected()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family");

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);
        Assert.Empty(template.Subtasks);

        await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(fixture, guardianToken, calendarId, template.Id, assignedTo: child.Id, expectedStatus: 400);
    }

    [Fact]
    public async Task Scheduling_from_a_nonexistent_template_is_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Personal");

        await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(fixture, guardianToken, calendarId, Guid.CreateVersion7(), expectedStatus: 404);
    }

    [Fact]
    public async Task Scheduling_from_a_template_belonging_to_an_unrelated_family_is_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Personal");

        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Sam");
        var otherTemplate = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, otherGuardianToken, otherChild.Id);
        Assert.NotNull(otherTemplate);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, otherGuardianToken, otherTemplate.Id);

        // ownerToken has no relationship at all to otherChild, so the unassigned-task owning-child
        // pivot (the caller themself) can never match otherTemplate's owner.
        await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(fixture, ownerToken, calendarId, otherTemplate.Id, expectedStatus: 404);
    }

    [Fact]
    public async Task Scheduling_a_siblings_template_for_a_different_child_is_not_found()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var sam = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Sam");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{alex.Id}");
            _.StatusCodeShouldBe(204);
        });
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{sam.Id}");
            _.StatusCodeShouldBe(204);
        });

        var samsTemplate = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, sam.Id);
        Assert.NotNull(samsTemplate);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, guardianToken, samsTemplate.Id);

        // Same guardian, same calendar, but Sam's template can't be scheduled for Alex -- task
        // templates are owned by one child, not shared across siblings.
        await CalendarTestHelpers.ScheduleTaskFromTemplateAsync(fixture, guardianToken, calendarId, samsTemplate.Id, assignedTo: alex.Id, expectedStatus: 404);
    }
}
