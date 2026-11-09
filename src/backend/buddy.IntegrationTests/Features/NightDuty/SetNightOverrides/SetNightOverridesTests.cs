using buddy.Common;
using buddy.Features.NightDuty;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.NightDuty.SetNightOverrides;

[Collection(BuddyApiCollection.Name)]
public sealed class SetNightOverridesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetNightOverrides")]
    public async Task An_override_wins_over_the_schedule_and_shows_what_the_schedule_said()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var monday = FutureMonday();
        await ReplacePatternAsync(fixture, family.FirstToken, family.Child.Id, monday, 1, monday,
            Day(0, DayOfWeek.Tuesday, Guardian(family.FirstId, "Screen night")));

        var nights = await SetOverridesAsync(fixture, family.FirstToken, family.Child.Id,
            Override(monday.AddDays(1), Guardian(family.SecondId, "Screen night")));

        var night = Assert.Single(nights);
        Assert.Equal(KindPlanned, night.Status.Kind);
        Assert.Equal(NightSource.Override, night.Status.Source);
        Assert.Equal(family.SecondId, night.Status.Entry!.Responsible.GuardianId);
        Assert.Equal("Screen night", night.Status.Entry.Note);
        Assert.Equal(family.FirstId, night.Status.Scheduled!.Responsible.GuardianId);

        // The week after still follows the schedule.
        var nextWeek = Assert.Single(await ListNightsAsync(fixture, family.FirstToken, family.Child.Id, monday.AddDays(8), monday.AddDays(8)));
        Assert.Equal(NightSource.Pattern, nextWeek.Status.Source);
        Assert.Equal(family.FirstId, nextWeek.Status.Entry!.Responsible.GuardianId);
    }

    [Fact]
    public async Task A_cleared_override_blanks_a_scheduled_night()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();
        await ReplacePatternAsync(fixture, token, child.Id, monday, 1, monday, Day(0, DayOfWeek.Monday, Guardian(guardianId)));

        var night = Assert.Single(await SetOverridesAsync(fixture, token, child.Id, Override(monday, entry: null)));

        Assert.Equal(KindCleared, night.Status.Kind);
        Assert.Null(night.Status.Entry);
        Assert.Equal(guardianId, night.Status.Scheduled!.Responsible.GuardianId);
    }

    [Fact]
    public async Task Switching_two_nights_appends_exactly_two_events_in_one_request()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var monday = FutureMonday();
        await ReplacePatternAsync(fixture, family.FirstToken, family.Child.Id, monday, 1, monday,
            Day(0, DayOfWeek.Tuesday, Guardian(family.SecondId)),
            Day(0, DayOfWeek.Thursday, Guardian(family.FirstId)));

        var store = fixture.Host.Services.GetRequiredService<INightScheduleEventStore>();
        var id = NightScheduleId.ForChild(new UserId(family.Child.Id));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        var nights = await SetOverridesAsync(fixture, family.FirstToken, family.Child.Id,
            Override(monday.AddDays(3), Guardian(family.SecondId)),
            Override(monday.AddDays(1), Guardian(family.FirstId)));

        var events = await store.ReadAsync(id, CancellationToken.None);
        Assert.Equal(before + 2, events.Count);
        Assert.Equal([monday.AddDays(1), monday.AddDays(3)], events.Skip(before).Select(e => ((NightOverridden)e.Value!).Date));
        Assert.All(events.Skip(before), e => Assert.Equal(new UserId(family.FirstId), ((NightOverridden)e.Value!).ModifiedBy));
        Assert.Equal([family.FirstId, family.SecondId], nights.OrderBy(n => n.Date).Select(n => n.Status.Entry!.Responsible.GuardianId!.Value));
    }

    [Fact]
    public async Task Setting_an_override_to_the_value_it_already_has_appends_no_event()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var date = FutureMonday();
        await SetOverridesAsync(fixture, token, child.Id, Override(date, Guardian(guardianId)));

        var store = fixture.Host.Services.GetRequiredService<INightScheduleEventStore>();
        var id = NightScheduleId.ForChild(new UserId(child.Id));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        await SetOverridesAsync(fixture, token, child.Id, Override(date, Guardian(guardianId)));

        Assert.Equal(before, (await store.ReadAsync(id, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_note_on_its_own_and_a_past_night_can_be_set()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var lastWeek = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);

        var night = Assert.Single(await SetOverridesAsync(fixture, token, child.Id, Override(lastWeek, NoteOnly("Movie night 🎬"))));

        Assert.Equal(KindPlanned, night.Status.Kind);
        Assert.Equal(ResponsibleUnassigned, night.Status.Entry!.Responsible.Kind);
        Assert.Equal("Movie night 🎬", night.Status.Entry.Note);
        Assert.Null(night.Status.Scheduled);
    }

    [Fact]
    public async Task A_change_by_one_guardian_is_visible_to_the_other()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var date = FutureMonday();

        await SetOverridesAsync(fixture, family.SecondToken, family.Child.Id, Override(date, Sleepover("Oscar's family")));

        var night = Assert.Single(await ListNightsAsync(fixture, family.FirstToken, family.Child.Id, date, date));
        Assert.Equal("Oscar's family", night.Status.Entry!.Responsible.HostName);
    }

    [Fact]
    public async Task Invalid_override_lists_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var (_, _, strangerId) = await fixture.CreateAuthenticatedUserAsync();
        var date = FutureMonday();

        await SetOverridesExpectingAsync(fixture, token, child.Id, 400);
        await SetOverridesExpectingAsync(fixture, token, child.Id, 400, Override(date, NoteOnly("a")), Override(date, NoteOnly("b")));
        await SetOverridesExpectingAsync(fixture, token, child.Id, 400,
            [.. Enumerable.Range(0, 32).Select(i => Override(date.AddDays(i), NoteOnly("x")))]);
        await SetOverridesExpectingAsync(fixture, token, child.Id, 400, Override(date, NoteOnly(new string('x', 61))));
        await SetOverridesExpectingAsync(fixture, token, child.Id, 400, Override(date, NoteOnly("")));
        await SetOverridesExpectingAsync(fixture, token, child.Id, 400, Override(date, Guardian(strangerId)));
    }

    [Fact]
    public async Task Nights_more_than_31_days_apart_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var date = FutureMonday();

        var response = await SetOverridesExpectingAsync(fixture, token, child.Id, 400,
            Override(date, NoteOnly("a")), Override(date.AddDays(32), NoteOnly("b")));

        Assert.Contains("nights", response.ReadAsJson<ErrorEnvelope>().Details.Keys);
        await SetOverridesExpectingAsync(fixture, token, child.Id, 200,
            Override(date, NoteOnly("a")), Override(date.AddDays(31), NoteOnly("b")));
    }

    [Fact]
    public async Task A_switch_can_move_a_babysitter_archived_since_but_not_pick_a_new_archived_one()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var anna = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");
        var freja = await BabysitterTestHelpers.AddAsync(fixture, token, "Freja");
        var date = FutureMonday();
        await SetOverridesAsync(fixture, token, child.Id, Override(date, Babysitter(guardianId, anna.Id)), Override(date.AddDays(1), Guardian(guardianId)));
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, anna.Id);
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, freja.Id);

        var switched = await SetOverridesAsync(fixture, token, child.Id,
            Override(date, Guardian(guardianId)), Override(date.AddDays(1), Babysitter(guardianId, anna.Id)));

        Assert.Equal("Anna", switched.Single(n => n.Date == date.AddDays(1)).Status.Entry!.Responsible.Name);
        await SetOverridesExpectingAsync(fixture, token, child.Id, 400, Override(date.AddDays(2), Babysitter(guardianId, freja.Id)));
    }

    [Fact]
    public async Task An_invalid_entry_is_reported_on_its_field()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var date = FutureMonday();

        var response = await SetOverridesExpectingAsync(fixture, token, child.Id, 400,
            Override(date, NoteOnly("ok")), Override(date.AddDays(1), NoteOnly(new string('x', 61))));

        Assert.Contains("nights[1].entry.note", response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var night = Override(FutureMonday(), Guardian(guardianId));

        await SetOverridesExpectingAsync(fixture, childToken, child.Id, 403, night);
        await SetOverridesExpectingAsync(fixture, strangerToken, child.Id, 404, night);
    }
}
