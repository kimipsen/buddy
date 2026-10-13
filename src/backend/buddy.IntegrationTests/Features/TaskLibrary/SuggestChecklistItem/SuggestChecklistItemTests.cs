using buddy.Features.TaskLibrary;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary.SuggestChecklistItem;

// A child suggests an item for their own packing list (docs/backend/analysis/packing-checklists.md,
// Question 8).
[Collection(BuddyApiCollection.Name)]
public sealed class SuggestChecklistItemTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SuggestChecklistItem")]
    public async Task The_owning_child_can_suggest_an_item()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        var response = await SuggestAsync(fixture, family.ChildToken, checklist.Id, "  Recorder ", ["Friday"]);

        var suggestion = Assert.Single(response.ReadAsJson<TaskTemplateDto>().Suggestions);
        Assert.Equal("Recorder", suggestion.Title);
        Assert.Equal([DayOfWeek.Friday], suggestion.Weekdays);
        Assert.Equal("Pending", suggestion.Status);
        Assert.Equal(family.Child.Id, suggestion.SuggestedBy);
        Assert.Null(suggestion.ResolvedAt);

        // The list itself is unchanged, and the guardian sees the suggestion with the template.
        var listed = Assert.Single(await ListAsync(fixture, family.GuardianToken, family.Child.Id));
        Assert.Single(listed.Subtasks);
        Assert.Equal(suggestion.Id, Assert.Single(listed.Suggestions).Id);
    }

    [Fact]
    public async Task A_guardian_is_forbidden_and_a_sibling_or_stranger_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, family.GuardianToken, "Ida");
        var siblingToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, sibling);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        await SuggestAsync(fixture, family.GuardianToken, checklist.Id, "Recorder", expectedStatus: 403);
        await SuggestAsync(fixture, siblingToken, checklist.Id, "Recorder", expectedStatus: 404);
        await SuggestAsync(fixture, strangerToken, checklist.Id, "Recorder", expectedStatus: 404);
    }

    [Fact]
    public async Task A_routine_takes_no_suggestions_and_an_archived_list_is_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var routine = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, family.GuardianToken, family.Child.Id);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        await SuggestAsync(fixture, family.ChildToken, routine!.Id, "Recorder", expectedStatus: 400);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Delete.Url($"/task-templates/{checklist.Id}");
            _.StatusCodeShouldBe(204);
        });

        await SuggestAsync(fixture, family.ChildToken, checklist.Id, "Recorder", expectedStatus: 404);
    }

    [Fact]
    public async Task Invalid_titles_and_an_empty_weekday_list_are_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        await SuggestAsync(fixture, family.ChildToken, checklist.Id, "   ", expectedStatus: 400);
        await SuggestAsync(fixture, family.ChildToken, checklist.Id, new string('x', 201), expectedStatus: 400);
        await SuggestAsync(fixture, family.ChildToken, checklist.Id, "Recorder", [], expectedStatus: 400);
    }

    [Fact]
    public async Task A_sixth_pending_suggestion_is_a_conflict()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        for (var i = 1; i <= ChecklistSuggestionRules.MaxPending; i++)
        {
            await SuggestAsync(fixture, family.ChildToken, checklist.Id, $"Idea {i}");
        }

        var response = await SuggestAsync(fixture, family.ChildToken, checklist.Id, "One too many", expectedStatus: 409);

        Assert.Equal(ChecklistSuggestionOutcome.TooManyPendingCode, ErrorCode(response));
    }

    [Fact]
    public async Task A_title_already_on_the_list_is_a_conflict_whatever_its_case_and_padding()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");

        var response = await SuggestAsync(fixture, family.ChildToken, checklist.Id, "  lunchBOX ", expectedStatus: 409);

        Assert.Equal(ChecklistSuggestionOutcome.ItemExistsCode, ErrorCode(response));
    }

    [Fact]
    public async Task Suggesting_a_pending_title_again_keeps_the_first_suggestion()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        await SuggestAsync(fixture, family.ChildToken, checklist.Id, "Recorder", ["Friday"]);
        var eventsBefore = (await ReadEventsAsync(fixture, checklist.Id)).Count;

        var again = await SuggestAsync(fixture, family.ChildToken, checklist.Id, "RECORDER", ["Monday"]);

        var suggestion = Assert.Single(again.ReadAsJson<TaskTemplateDto>().Suggestions);
        Assert.Equal("Recorder", suggestion.Title);
        Assert.Equal([DayOfWeek.Friday], suggestion.Weekdays);
        Assert.Equal(eventsBefore, (await ReadEventsAsync(fixture, checklist.Id)).Count);
    }

    [Fact]
    public async Task A_title_that_was_declined_before_can_be_suggested_again()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var first = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");
        await ResolveAsync(fixture, family.GuardianToken, checklist.Id, first, "decline");

        var again = await SuggestAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        var suggestions = again.ReadAsJson<TaskTemplateDto>().Suggestions;
        Assert.Equal(2, suggestions.Count);
        Assert.Equal("Pending", suggestions[0].Status);
        Assert.NotEqual(first, suggestions[0].Id);
        Assert.Equal("Declined", suggestions[1].Status);
    }

    [Fact]
    public async Task A_replay_with_the_same_idempotency_key_appends_nothing()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var key = Guid.NewGuid().ToString();

        var first = await SuggestAsync(fixture, family.ChildToken, checklist.Id, "Recorder", idempotencyKey: key);
        var eventsAfterFirst = (await ReadEventsAsync(fixture, checklist.Id)).Count;
        var replay = await SuggestAsync(fixture, family.ChildToken, checklist.Id, "Recorder", idempotencyKey: key);

        Assert.Equal(await first.ReadAsTextAsync(), await replay.ReadAsTextAsync());
        Assert.Equal(eventsAfterFirst, (await ReadEventsAsync(fixture, checklist.Id)).Count);
    }
}
