using Alba;

using buddy.IntegrationTests.Fixtures;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary.AddSubtask;

// A checklist's items have no duration and may apply on some weekdays only; a routine's steps still
// need a duration (docs/backend/analysis/packing-checklists.md, Questions 2 and 3).
[Collection(BuddyApiCollection.Name)]
public sealed class ChecklistItemTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_checklist_item_has_no_duration_and_keeps_its_weekdays()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);

        var withItems = await AddItemAsync(fixture, family.GuardianToken, checklist.Id, "Lunchbox");
        withItems = await AddItemAsync(fixture, family.GuardianToken, checklist.Id, "Gym clothes", ["Thursday", "Tuesday"]);

        Assert.NotNull(withItems);
        Assert.Equal(TimeSpan.Zero, withItems.TotalDuration);
        Assert.Empty(withItems.Subtasks[0].Weekdays);
        Assert.Equal([DayOfWeek.Tuesday, DayOfWeek.Thursday], withItems.Subtasks[1].Weekdays);
    }

    [Fact]
    public async Task Every_day_of_the_week_reads_back_as_no_weekday_filter()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);

        var withItem = await AddItemAsync(
            fixture, family.GuardianToken, checklist.Id, "Glasses", ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]);

        Assert.Empty(Assert.Single(withItem!.Subtasks).Weekdays);
    }

    [Fact]
    public async Task An_empty_weekday_list_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);

        await AddItemAsync(fixture, family.GuardianToken, checklist.Id, "Lunchbox", [], expectedStatus: 400);
    }

    [Fact]
    public async Task A_checklist_item_with_a_duration_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);

        await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, family.GuardianToken, checklist.Id, new AddSubtaskOptions("Lunchbox", Duration: "00:02:00"), expectedStatus: 400);
    }

    [Fact]
    public async Task A_routine_step_without_a_duration_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        var routine = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, family.GuardianToken, family.Child.Id);

        await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, family.GuardianToken, routine!.Id, new AddSubtaskOptions("Brush teeth", Duration: "00:00:00"), expectedStatus: 400);
    }

    [Fact]
    public async Task Updating_an_item_changes_its_weekdays_and_keeps_the_zero_duration_rule()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);
        var withItem = await AddItemAsync(fixture, family.GuardianToken, checklist.Id, "Swimming kit", ["Thursday"]);
        var itemId = Assert.Single(withItem!.Subtasks).Id;
        var url = $"/task-templates/{checklist.Id}/subtasks/{itemId}";

        var updated = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Patch.Json(new { Title = "Swimming kit and towel", Weekdays = new[] { "Wednesday", "Friday" } }).ToUrl(url);
            _.StatusCodeShouldBeOk();
        });

        var item = Assert.Single(updated.ReadAsJson<TaskTemplateDto>().Subtasks);
        Assert.Equal("Swimming kit and towel", item.Title);
        Assert.Equal([DayOfWeek.Wednesday, DayOfWeek.Friday], item.Weekdays);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Patch.Json(new { Title = "Swimming kit", Duration = "00:05:00" }).ToUrl(url);
            _.StatusCodeShouldBe(400);
        });
    }
}
