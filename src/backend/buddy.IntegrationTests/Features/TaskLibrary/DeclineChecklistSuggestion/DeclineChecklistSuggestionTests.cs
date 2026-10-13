using buddy.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary.DeclineChecklistSuggestion;

[Collection(BuddyApiCollection.Name)]
public sealed class DeclineChecklistSuggestionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeclineChecklistSuggestion")]
    public async Task Declining_leaves_the_list_alone_and_repeating_it_appends_nothing()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        var response = await ResolveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "decline");

        var template = response.ReadAsJson<TaskTemplateDto>();
        Assert.Single(template.Subtasks);
        var suggestion = Assert.Single(template.Suggestions);
        Assert.Equal("Declined", suggestion.Status);
        Assert.Null(suggestion.SubtaskId);

        var eventsBefore = (await ReadEventsAsync(fixture, checklist.Id)).Count;
        await ResolveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "decline");
        Assert.Equal(eventsBefore, (await ReadEventsAsync(fixture, checklist.Id)).Count);
    }

    [Fact]
    public async Task Declining_an_approved_suggestion_is_a_conflict()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");
        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Recorder");

        var response = await ResolveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "decline", expectedStatus: 409);

        Assert.Equal(ChecklistSuggestionOutcome.AlreadyResolvedCode, ErrorCode(response));
    }

    [Fact]
    public async Task The_child_is_forbidden_and_a_stranger_or_unknown_suggestion_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        await ResolveAsync(fixture, family.ChildToken, checklist.Id, suggestionId, "decline", expectedStatus: 403);
        await ResolveAsync(fixture, strangerToken, checklist.Id, suggestionId, "decline", expectedStatus: 404);
        await ResolveAsync(fixture, family.GuardianToken, checklist.Id, Guid.CreateVersion7(), "decline", expectedStatus: 404);
    }
}
