using buddy.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.TaskLibrary.ChecklistTestHelpers;

namespace buddy.IntegrationTests.Features.TaskLibrary.ApproveChecklistSuggestion;

[Collection(BuddyApiCollection.Name)]
public sealed class ApproveChecklistSuggestionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ApproveChecklistSuggestion")]
    public async Task Approving_adds_an_item_with_the_guardians_title_and_days()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox", "Glasses");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "recorder", ["Monday"]);

        var response = await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Recorder", ["Friday"]);

        var template = response.ReadAsJson<TaskTemplateDto>();
        var item = template.Subtasks[^1];
        Assert.Equal("Recorder", item.Title);
        Assert.Equal([DayOfWeek.Friday], item.Weekdays);
        Assert.Equal(TimeSpan.Zero, item.Duration);
        Assert.Null(item.Icon);

        var suggestion = Assert.Single(template.Suggestions);
        Assert.Equal("Approved", suggestion.Status);
        Assert.Equal(item.Id, suggestion.SubtaskId);
        Assert.NotNull(suggestion.ResolvedAt);
    }

    [Fact]
    public async Task The_position_is_honoured()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox", "Glasses");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        var response = await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Recorder", position: 0);

        Assert.Equal(["Recorder", "Lunchbox", "Glasses"], response.ReadAsJson<TaskTemplateDto>().Subtasks.Select(s => s.Title));
    }

    [Fact]
    public async Task Approving_twice_adds_the_item_once()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Recorder");
        var again = await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Something else");
        // A repeat ignores its body, even an invalid one.
        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, " ");

        Assert.Equal(2, again.ReadAsJson<TaskTemplateDto>().Subtasks.Count);
        Assert.Single(await ReadEventsAsync(fixture, checklist.Id), e => e.Value is ChecklistItemSuggestionApproved);
    }

    [Fact]
    public async Task Approving_a_declined_or_withdrawn_suggestion_is_a_conflict()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var declined = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");
        var withdrawn = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Sun hat");
        await ResolveAsync(fixture, family.GuardianToken, checklist.Id, declined, "decline");
        await ResolveAsync(fixture, family.ChildToken, checklist.Id, withdrawn, "withdraw");

        var first = await ApproveAsync(fixture, family.GuardianToken, checklist.Id, declined, "Recorder", expectedStatus: 409);
        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, withdrawn, "Sun hat", expectedStatus: 409);

        Assert.Equal(ChecklistSuggestionOutcome.AlreadyResolvedCode, ErrorCode(first));
    }

    [Fact]
    public async Task An_invalid_title_or_empty_weekday_list_is_rejected()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, " ", expectedStatus: 400);
        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Recorder", [], expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_is_forbidden_and_a_stranger_or_unknown_suggestion_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        await ApproveAsync(fixture, family.ChildToken, checklist.Id, suggestionId, "Recorder", expectedStatus: 403);
        await ApproveAsync(fixture, strangerToken, checklist.Id, suggestionId, "Recorder", expectedStatus: 404);
        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, Guid.CreateVersion7(), "Recorder", expectedStatus: 404);
    }

    [Fact]
    public async Task A_guardian_whose_link_was_revoked_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var checklist = await ChecklistWithItemsAsync(fixture, family, "Lunchbox");
        var suggestionId = await SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await ApproveAsync(fixture, family.GuardianToken, checklist.Id, suggestionId, "Recorder", expectedStatus: 404);
    }
}
