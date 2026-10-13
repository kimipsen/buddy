using System.Text.Json;

using Alba;

using buddy.Common.Erasure;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary;

// Suggestions are the child's own text (docs/backend/analysis/packing-checklists.md, Question 8,
// "GDPR"): the export carries every one, and a list that passes to an heir loses them.
[Collection(BuddyApiCollection.Name)]
public sealed class ChecklistSuggestionPrivacyTests(BuddyApiFixture fixture)
{
    private TaskLibraryPersonalDataEraser Eraser =>
        fixture.Host.Services.GetServices<IPersonalDataEraser>().OfType<TaskLibraryPersonalDataEraser>().Single();

    [Fact]
    public async Task A_list_passing_to_an_heir_withdraws_pending_suggestions_and_masks_every_suggestion_title()
    {
        var family = await CreateFamilyAsync(fixture);
        var heir = await GuardianTestHelpers.CreateChildAsync(fixture, family.GuardianToken, "Ida");
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var approved = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");
        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, approved, "Recorder");
        var pending = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Secret diary");

        await Eraser.EraseChildAsync(new ErasureSubject(new UserId(family.Child.Id), null), new UserId(heir.Id), CancellationToken.None);

        var events = await ReadEventsAsync(fixture, checklist.Id);
        Assert.All(events.Select(e => e.Value).OfType<ChecklistItemSuggested>(), e => Assert.Equal(Erased.Text, e.Title));
        Assert.Contains(events.Select(e => e.Value).OfType<ChecklistItemSuggestionWithdrawn>(), e => e.SuggestionId.Value == pending);

        var listed = Assert.Single(await ListAsync(fixture, family.GuardianToken, heir.Id));
        Assert.Equal(["Lunchbox", "Recorder"], listed.Subtasks.Select(s => s.Title));
        Assert.All(listed.Suggestions, s => Assert.Equal(Erased.Text, s.Title));
        Assert.Equal("Withdrawn", listed.Suggestions.Single(s => s.Id == pending).Status);
    }

    [Fact]
    public async Task Without_an_heir_the_list_goes_with_the_child()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        await Eraser.EraseChildAsync(new ErasureSubject(new UserId(family.Child.Id), null), heir: null, CancellationToken.None);

        Assert.Empty(await ReadEventsAsync(fixture, checklist.Id));
    }

    [Fact]
    public async Task The_export_lists_every_suggestion_beyond_the_responses_history()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var total = TaskTemplateResponse.ResolvedSuggestionHistory + 1;

        for (var i = 0; i < total; i++)
        {
            var id = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, $"Idea {i}");
            await ResolveAsync(fixture, family.GuardianToken, checklist.Id, id, "decline");
        }

        var listed = Assert.Single(await ListAsync(fixture, family.GuardianToken, family.Child.Id));
        Assert.Equal(TaskTemplateResponse.ResolvedSuggestionHistory, listed.Suggestions.Count);
        Assert.Equal(listed.Suggestions.OrderByDescending(s => s.ResolvedAt), listed.Suggestions);

        var export = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Get.Url("/users/me/export");
            _.StatusCodeShouldBeOk();
        });

        using var json = JsonDocument.Parse(await export.ReadAsTextAsync());
        var child = Assert.Single(json.RootElement.GetProperty("sections").GetProperty("taskLibrary").EnumerateArray());
        var template = Assert.Single(child.GetProperty("templates").EnumerateArray());
        Assert.Equal(total, template.GetProperty("suggestions").GetArrayLength());
    }
}
