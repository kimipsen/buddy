using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary.CopyTaskTemplate;

// Copying a routine or a packing list to a sibling (docs/backend/analysis/packing-checklists.md,
// Question 9).
[Collection(BuddyApiCollection.Name)]
public sealed class CopyTaskTemplateTests(BuddyApiFixture fixture)
{
    private Task<IScenarioResult> CopyAsync(string token, Guid templateId, Guid childId, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { ChildId = childId }).ToUrl($"/task-templates/{templateId}/copy");
            _.StatusCodeShouldBe(expectedStatus);
        });

    [Fact]
    [CoversEndpoint("CopyTaskTemplate")]
    public async Task A_packing_list_is_copied_to_a_sibling_with_its_items_and_options_but_not_its_suggestions()
    {
        var family = await CreateFamilyAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, family.GuardianToken, "Ida");
        var source = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id, eveningBeforeFrom: "17:00:00");
        await AddItemAsync(fixture, family.GuardianToken, source.Id, "Lunchbox");
        source = (await AddItemAsync(fixture, family.GuardianToken, source.Id, "Gym clothes", ["Tuesday"]))!;
        await SuggestAsync(fixture, family.ChildToken, source.Id, "Recorder");

        var copy = (await CopyAsync(family.GuardianToken, source.Id, sibling.Id)).ReadAsJson<TaskTemplateDto>();

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(source.Name, copy.Name);
        Assert.Equal(source.Icon, copy.Icon);
        Assert.Equal(source.Color, copy.Color);
        Assert.Equal("Checklist", copy.Kind);
        Assert.Equal(new TimeOnly(17, 0), copy.Checklist?.EveningBeforeFrom);
        Assert.Equal(source.Subtasks.Select(s => (s.Title, s.Duration, s.Icon, string.Join(',', s.Weekdays))),
            copy.Subtasks.Select(s => (s.Title, s.Duration, s.Icon, string.Join(',', s.Weekdays))));
        Assert.Empty(copy.Subtasks.Select(s => s.Id).Intersect(source.Subtasks.Select(s => s.Id)));
        Assert.Empty(copy.Suggestions);

        // The copy is the sibling's; the source child's library is unchanged.
        Assert.Equal(copy.Id, Assert.Single(await ListAsync(fixture, family.GuardianToken, sibling.Id)).Id);
        var unchanged = Assert.Single(await ListAsync(fixture, family.GuardianToken, family.Child.Id));
        Assert.Equal(source.Id, unchanged.Id);
        Assert.Single(unchanged.Suggestions);
    }

    [Fact]
    public async Task A_routine_is_copied_with_its_durations_and_order()
    {
        var family = await CreateFamilyAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, family.GuardianToken, "Ida");
        var routine = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, family.GuardianToken, family.Child.Id);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, family.GuardianToken, routine!.Id, new AddSubtaskOptions("Brush teeth", Duration: "00:02:00"));
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, family.GuardianToken, routine.Id, new AddSubtaskOptions("Get dressed", Duration: "00:10:00"));

        var copy = (await CopyAsync(family.GuardianToken, routine.Id, sibling.Id)).ReadAsJson<TaskTemplateDto>();

        Assert.Equal("Routine", copy.Kind);
        Assert.Null(copy.Checklist);
        Assert.Equal(["Brush teeth", "Get dressed"], copy.Subtasks.Select(s => s.Title));
        Assert.Equal(TimeSpan.FromMinutes(12), copy.TotalDuration);
    }

    [Fact]
    public async Task Copying_to_the_same_child_makes_a_duplicate()
    {
        var family = await CreateFamilyAsync(fixture);
        var source = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        await CopyAsync(family.GuardianToken, source.Id, family.Child.Id);

        Assert.Equal(2, (await ListAsync(fixture, family.GuardianToken, family.Child.Id)).Count);
    }

    [Fact]
    public async Task The_sources_child_is_forbidden_and_a_stranger_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var source = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        await CopyAsync(family.ChildToken, source.Id, family.Child.Id, expectedStatus: 403);
        await CopyAsync(strangerToken, source.Id, family.Child.Id, expectedStatus: 404);
    }

    [Fact]
    public async Task A_target_child_the_guardian_is_not_linked_to_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var other = await CreateFamilyAsync(fixture, "Otto");
        var source = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        await CopyAsync(family.GuardianToken, source.Id, other.Child.Id, expectedStatus: 404);
        await CopyAsync(family.GuardianToken, source.Id, Guid.CreateVersion7(), expectedStatus: 404);
        Assert.Empty(await ListAsync(fixture, other.GuardianToken, other.Child.Id));
    }

    [Fact]
    public async Task An_archived_source_gets_not_found_and_an_empty_child_id_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        var source = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        var rejected = await CopyAsync(family.GuardianToken, source.Id, Guid.Empty, expectedStatus: 400);
        Assert.Equal(["childId"], rejected.ReadAsJson<ErrorEnvelope>().Details.Keys);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Delete.Url($"/task-templates/{source.Id}");
            _.StatusCodeShouldBe(204);
        });

        await CopyAsync(family.GuardianToken, source.Id, family.Child.Id, expectedStatus: 404);
    }
}
