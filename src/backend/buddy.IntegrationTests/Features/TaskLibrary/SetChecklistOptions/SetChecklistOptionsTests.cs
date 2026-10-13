using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary.SetChecklistOptions;

// Creating a packing list (a Checklist template) and its "evening before" option
// (docs/backend/analysis/packing-checklists.md, Questions 2 and 4).
[Collection(BuddyApiCollection.Name)]
public sealed class SetChecklistOptionsTests(BuddyApiFixture fixture)
{
    private Task<IScenarioResult> SetAsync(string token, Guid templateId, string? eveningBeforeFrom, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { EveningBeforeFrom = eveningBeforeFrom }).ToUrl($"/task-templates/{templateId}/checklist-options");
            _.StatusCodeShouldBe(expectedStatus);
        });

    [Fact]
    public async Task A_guardian_can_create_a_packing_list_shown_the_evening_before()
    {
        var family = await CreateFamilyAsync(fixture);

        var created = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id, eveningBeforeFrom: "17:00:00");

        Assert.Equal("Checklist", created.Kind);
        Assert.Equal(new TimeOnly(17, 0), created.Checklist?.EveningBeforeFrom);
        Assert.Empty(created.Suggestions);

        var listed = Assert.Single(await ListAsync(fixture, family.ChildToken, family.Child.Id));
        Assert.Equal("Checklist", listed.Kind);
        Assert.Equal(new TimeOnly(17, 0), listed.Checklist?.EveningBeforeFrom);
    }

    [Fact]
    public async Task A_routine_is_still_the_default_kind_and_has_no_checklist_options()
    {
        var family = await CreateFamilyAsync(fixture);

        var routine = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, family.GuardianToken, family.Child.Id);

        Assert.NotNull(routine);
        Assert.Equal("Routine", routine.Kind);
        Assert.Null(routine.Checklist);
    }

    [Fact]
    public async Task A_routine_cannot_be_created_with_an_evening_before_time()
    {
        var family = await CreateFamilyAsync(fixture);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Post.Json(new { Name = "Morning", Icon = "sunrise", Color = "#ffaa00", EveningBeforeFrom = "17:00:00" })
                .ToUrl($"/task-templates/children/{family.Child.Id}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    [CoversEndpoint("SetChecklistOptions")]
    public async Task A_guardian_can_change_and_clear_the_evening_before_time()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);
        Assert.Null(checklist.Checklist?.EveningBeforeFrom);

        var set = (await SetAsync(family.GuardianToken, checklist.Id, "18:30:00")).ReadAsJson<TaskTemplateDto>();
        Assert.Equal(new TimeOnly(18, 30), set.Checklist?.EveningBeforeFrom);

        var cleared = (await SetAsync(family.GuardianToken, checklist.Id, null)).ReadAsJson<TaskTemplateDto>();
        Assert.Null(cleared.Checklist?.EveningBeforeFrom);
        Assert.NotNull(cleared.Checklist);
    }

    [Fact]
    public async Task Setting_the_same_options_again_appends_nothing()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id, eveningBeforeFrom: "17:00:00");
        var before = (await ReadEventsAsync(fixture, checklist.Id)).Count;

        await SetAsync(family.GuardianToken, checklist.Id, "17:00:00");

        Assert.Equal(before, (await ReadEventsAsync(fixture, checklist.Id)).Count);
    }

    [Fact]
    public async Task A_routine_has_no_checklist_options_to_set()
    {
        var family = await CreateFamilyAsync(fixture);
        var routine = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, family.GuardianToken, family.Child.Id);

        await SetAsync(family.GuardianToken, routine!.Id, "17:00:00", expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_is_forbidden_and_a_stranger_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var checklist = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);

        await SetAsync(family.ChildToken, checklist.Id, "17:00:00", expectedStatus: 403);
        await SetAsync(strangerToken, checklist.Id, "17:00:00", expectedStatus: 404);
    }
}
