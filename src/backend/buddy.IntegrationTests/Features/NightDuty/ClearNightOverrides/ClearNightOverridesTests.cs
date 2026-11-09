using buddy.Features.NightDuty;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.NightDuty.ClearNightOverrides;

[Collection(BuddyApiCollection.Name)]
public sealed class ClearNightOverridesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ClearNightOverrides")]
    public async Task Clearing_an_override_falls_back_to_the_schedule()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();
        await ReplacePatternAsync(fixture, token, child.Id, monday, 1, monday, Day(0, DayOfWeek.Monday, Guardian(guardianId)));
        await SetOverridesAsync(fixture, token, child.Id, Override(monday, entry: null), Override(monday.AddDays(1), NoteOnly("Sleepover party")));

        await ClearOverridesAsync(fixture, token, child.Id, monday, monday.AddDays(1));

        var nights = await ListNightsAsync(fixture, token, child.Id, monday, monday.AddDays(1));
        Assert.Equal(NightSource.Pattern, nights[0].Status.Source);
        Assert.Equal(guardianId, nights[0].Status.Entry!.Responsible.GuardianId);
        Assert.Equal(KindUnplanned, nights[1].Status.Kind);
    }

    [Fact]
    public async Task Clearing_a_range_without_overrides_is_a_no_op()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await ClearOverridesAsync(fixture, token, child.Id, FutureMonday(), FutureMonday().AddDays(6));
    }

    [Fact]
    public async Task An_invalid_range_is_rejected_the_child_is_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var monday = FutureMonday();

        await ClearOverridesAsync(fixture, token, child.Id, monday, monday.AddDays(-1), 400);
        await ClearOverridesAsync(fixture, token, child.Id, monday, monday.AddDays(32), 400);
        await ClearOverridesAsync(fixture, childToken, child.Id, monday, monday, 403);
        await ClearOverridesAsync(fixture, strangerToken, child.Id, monday, monday, 404);
    }
}
