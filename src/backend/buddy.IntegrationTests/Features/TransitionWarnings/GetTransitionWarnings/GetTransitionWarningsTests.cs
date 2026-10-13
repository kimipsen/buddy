using buddy.Features.TransitionWarnings;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.TransitionWarnings.TransitionWarningsTestHelpers;

namespace buddy.IntegrationTests.Features.TransitionWarnings.GetTransitionWarnings;

[Collection(BuddyApiCollection.Name)]
public sealed class GetTransitionWarningsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetMyTransitionWarnings")]
    public async Task A_child_no_guardian_has_configured_reads_warnings_off_with_15_5_1_and_every_kind_pre_filled()
    {
        var family = await CreateFamilyAsync(fixture);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);

        var mine = await GetMineAsync(fixture, childToken);

        Assert.False(mine.Enabled);
        Assert.Equal([15, 5, 1], mine.LeadMinutes);
        Assert.Equal(AllKinds, mine.Kinds);
        Assert.Null(mine.QuietHours);
        Assert.Empty(mine.Overrides);
    }

    [Fact]
    [CoversEndpoint("GetChildTransitionWarnings")]
    public async Task A_guardian_reads_the_same_defaults_for_a_child_with_no_settings()
    {
        var family = await CreateFamilyAsync(fixture);

        var settings = await GetForChildAsync(fixture, family.GuardianToken, family.Child.Id);

        Assert.NotNull(settings);
        Assert.Equal(family.Child.Id, settings.ChildId);
        Assert.False(settings.Enabled);
        Assert.Equal([15, 5, 1], settings.LeadMinutes);
        Assert.Equal(AllKinds, settings.Kinds);
        Assert.Empty(settings.Overrides);
    }

    [Fact]
    public async Task The_child_reads_what_a_guardian_saved_with_override_item_ids_and_no_titles()
    {
        var family = await CreateFamilyAsync(fixture);
        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id,
            Settings(true, [10, 2], [WarningKind.Event], new QuietHoursDto(new TimeOnly(21, 0), new TimeOnly(7, 0))));
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, [30]);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);

        var mine = await GetMineAsync(fixture, childToken);

        Assert.True(mine.Enabled);
        Assert.Equal([10, 2], mine.LeadMinutes);
        Assert.Equal([WarningKind.Event], mine.Kinds);
        Assert.Equal(new QuietHoursDto(new TimeOnly(21, 0), new TimeOnly(7, 0)), mine.QuietHours);
        var single = Assert.Single(mine.Overrides);
        Assert.Equal(family.Event.Id, single.ItemId);
        Assert.Equal([30], single.LeadMinutes);
    }

    [Fact]
    public async Task The_child_is_forbidden_from_the_guardian_view_of_their_own_settings()
    {
        var family = await CreateFamilyAsync(fixture);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, family.Child);

        await GetForChildAsync(fixture, childToken, family.Child.Id, expectedStatus: 403);
    }

    [Fact]
    public async Task A_guardian_of_another_family_gets_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await GetForChildAsync(fixture, strangerToken, family.Child.Id, expectedStatus: 404);
        await GetForChildAsync(fixture, family.GuardianToken, Guid.CreateVersion7(), expectedStatus: 404);
    }

    [Fact]
    public async Task A_revoked_guardian_link_drops_to_not_found()
    {
        var family = await CreateFamilyAsync(fixture);
        await RevokeLinkAsync(fixture, family.GuardianToken, family.Child.Id);

        await GetForChildAsync(fixture, family.GuardianToken, family.Child.Id, expectedStatus: 404);
        await ConfigureAsync(fixture, family.GuardianToken, family.Child.Id, Settings(true, [5]), expectedStatus: 404);
    }

    [Fact]
    public async Task An_override_for_a_deleted_item_is_left_out_of_the_guardian_list()
    {
        var family = await CreateFamilyAsync(fixture);
        await SetOverrideAsync(fixture, family.GuardianToken, family.Child.Id, family.Event.Id, []);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.GuardianToken}");
            _.Delete.Url($"/calendars/{family.CalendarId}/items/{family.Event.Id}");
            _.StatusCodeShouldBe(204);
        });

        var settings = await GetForChildAsync(fixture, family.GuardianToken, family.Child.Id);
        Assert.NotNull(settings);
        Assert.Empty(settings.Overrides);
    }
}
