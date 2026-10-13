using buddy.Features.TaskLibrary;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary.WithdrawChecklistSuggestion;

[Collection(BuddyApiCollection.Name)]
public sealed class WithdrawChecklistSuggestionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("WithdrawChecklistSuggestion")]
    public async Task The_child_can_take_back_a_pending_suggestion_and_repeating_it_appends_nothing()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        var response = await ResolveAsync(fixture, family.ChildToken, checklist.Id, suggestionId, "withdraw");

        Assert.Equal("Withdrawn", Assert.Single(response.ReadAsJson<TaskTemplateDto>().Suggestions).Status);

        var eventsBefore = (await ReadEventsAsync(fixture, checklist.Id)).Count;
        await ResolveAsync(fixture, family.ChildToken, checklist.Id, suggestionId, "withdraw");
        Assert.Equal(eventsBefore, (await ReadEventsAsync(fixture, checklist.Id)).Count);
    }

    [Fact]
    public async Task Withdrawing_an_approved_suggestion_is_a_conflict()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");
        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Recorder");

        var response = await ResolveAsync(fixture, family.ChildToken, checklist.Id, suggestionId, "withdraw", expectedStatus: 409);

        Assert.Equal(ChecklistSuggestionOutcome.AlreadyResolvedCode, ErrorCode(response));
    }

    [Fact]
    public async Task A_guardian_is_forbidden_and_a_sibling_or_unknown_suggestion_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, family.GuardianToken, "Ida");
        var siblingToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, sibling);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        await ResolveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "withdraw", expectedStatus: 403);
        await ResolveAsync(fixture, siblingToken, checklist.Id, suggestionId, "withdraw", expectedStatus: 404);
        await ResolveAsync(fixture, family.ChildToken, checklist.Id, Guid.CreateVersion7(), "withdraw", expectedStatus: 404);
    }
}
