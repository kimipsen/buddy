using buddy.Features.SleepDiaries;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.SleepDiaries.SleepDiaryTestHelpers;

namespace buddy.IntegrationTests.Features.SleepDiaries.ListSleepDiaryEntries;

[Collection(BuddyApiCollection.Name)]
public sealed class ListSleepDiaryEntriesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListSleepDiaryEntries")]
    public async Task Only_logged_days_inside_the_range_come_back_in_date_order()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        await LogAsync(fixture, token, child.Id, Monday.AddDays(3), FullNight());
        await LogAsync(fixture, token, child.Id, Monday, FullNight());
        await LogAsync(fixture, token, child.Id, Monday.AddDays(20), FullNight());

        var diary = await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(13));

        Assert.Equal([Monday, Monday.AddDays(3)], diary!.Entries.Select(e => e.Date));
    }

    [Fact]
    public async Task A_child_with_no_diary_gets_an_empty_list()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        var diary = await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(13));

        Assert.Empty(diary!.Entries);
        Assert.Equal("", diary.SleepHygieneNotes);
    }

    [Fact]
    public async Task An_inverted_or_too_long_range_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(-1), expectedStatus: 400);
        await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(ListSleepDiaryEntriesHandler.MaxRangeDays + 1), expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_and_an_unrelated_user_get_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await LogAsync(fixture, token, child.Id, Monday, FullNight());

        await ListAsync(fixture, childToken, child.Id, Monday, Monday, expectedStatus: 404);
        await ListAsync(fixture, strangerToken, child.Id, Monday, Monday, expectedStatus: 404);
    }
}
