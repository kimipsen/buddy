using buddy.Features.SleepDiaries;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.SleepDiaries.SleepDiaryTestHelpers;

namespace buddy.IntegrationTests.Features.SleepDiaries.UpdateSleepHygieneNotes;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateSleepHygieneNotesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateSleepHygieneNotes")]
    public async Task Notes_written_before_any_night_start_the_diary_and_read_back()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await UpdateNotesAsync(fixture, token, child.Id, "  No screens after 19:00, blackout curtains  ");

        var diary = await ListAsync(fixture, token, child.Id, Monday, Monday);
        Assert.Equal("No screens after 19:00, blackout curtains", diary!.SleepHygieneNotes);
        Assert.Empty(diary.Entries);
    }

    [Fact]
    public async Task Notes_can_be_changed_and_cleared()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        await LogAsync(fixture, token, child.Id, Monday, FullNight());

        await UpdateNotesAsync(fixture, token, child.Id, "Melatonin from week 2");
        await UpdateNotesAsync(fixture, token, child.Id, "Melatonin from week 2");
        Assert.Equal("Melatonin from week 2", (await ListAsync(fixture, token, child.Id, Monday, Monday))!.SleepHygieneNotes);

        await UpdateNotesAsync(fixture, token, child.Id, null);
        Assert.Equal("", (await ListAsync(fixture, token, child.Id, Monday, Monday))!.SleepHygieneNotes);
    }

    [Fact]
    public async Task Overlong_notes_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await UpdateNotesAsync(fixture, token, child.Id, new string('x', UpdateSleepHygieneNotesValidator.MaxNotesLength + 1), expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_and_an_unrelated_user_get_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await UpdateNotesAsync(fixture, childToken, child.Id, "x", expectedStatus: 404);
        await UpdateNotesAsync(fixture, strangerToken, child.Id, "x", expectedStatus: 404);
    }
}
