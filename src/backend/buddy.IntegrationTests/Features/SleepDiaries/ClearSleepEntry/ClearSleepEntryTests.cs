using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.SleepDiaries.SleepDiaryTestHelpers;

namespace buddy.IntegrationTests.Features.SleepDiaries.ClearSleepEntry;

[Collection(BuddyApiCollection.Name)]
public sealed class ClearSleepEntryTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ClearSleepEntry")]
    public async Task Clearing_a_day_removes_only_that_entry()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        await LogAsync(fixture, token, child.Id, Monday, FullNight());
        await LogAsync(fixture, token, child.Id, Monday.AddDays(1), FullNight());

        await ClearAsync(fixture, token, child.Id, Monday);

        var entry = Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(1)))!.Entries);
        Assert.Equal(Monday.AddDays(1), entry.Date);
    }

    [Fact]
    public async Task Clearing_a_day_with_no_entry_is_idempotent()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        // No diary stream at all yet.
        await ClearAsync(fixture, token, child.Id, Monday);

        await LogAsync(fixture, token, child.Id, Monday, FullNight());
        await ClearAsync(fixture, token, child.Id, Monday);
        await ClearAsync(fixture, token, child.Id, Monday);
    }

    [Fact]
    public async Task The_child_and_an_unrelated_user_get_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await LogAsync(fixture, token, child.Id, Monday, FullNight());

        await ClearAsync(fixture, childToken, child.Id, Monday, expectedStatus: 404);
        await ClearAsync(fixture, strangerToken, child.Id, Monday, expectedStatus: 404);

        Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday))!.Entries);
    }
}
