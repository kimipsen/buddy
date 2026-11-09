using buddy.Common;
using buddy.Features.NightDuty;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.NightDuty.ReplaceNightPattern;

[Collection(BuddyApiCollection.Name)]
public sealed class ReplaceNightPatternTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ReplaceNightPattern")]
    public async Task An_alternating_two_week_schedule_resolves_per_cycle_week()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();

        // Week A: Monday with the guardian, Friday a screen night. Week B: Saturday at Grandma's.
        var schedule = await ReplacePatternAsync(fixture, token, child.Id, monday, 2, monday,
            Day(1, DayOfWeek.Saturday, Sleepover("Grandma Lis", "Skovvej 12")),
            Day(0, DayOfWeek.Friday, NoteOnly("Screen night 📱")),
            Day(0, DayOfWeek.Monday, Guardian(guardianId)));

        var version = Assert.Single(schedule.Patterns);
        Assert.Equal(monday, version.EffectiveFrom);
        Assert.Equal(NightPatternStatus.Upcoming, version.Status);
        Assert.Equal(
            [(0, DayOfWeek.Monday), (0, DayOfWeek.Friday), (1, DayOfWeek.Saturday)],
            version.Days.Select(d => (d.Week, d.Day)));

        var nights = await ListNightsAsync(fixture, token, child.Id, monday, monday.AddDays(20));
        var planned = nights.Where(n => n.Status.Kind == KindPlanned).ToDictionary(n => n.Date);

        Assert.Equal([monday, monday.AddDays(4), monday.AddDays(12), monday.AddDays(14), monday.AddDays(18)], planned.Keys.Order());
        Assert.Equal(guardianId, planned[monday].Status.Entry!.Responsible.GuardianId);
        Assert.Equal("Screen night 📱", planned[monday.AddDays(4)].Status.Entry!.Note);
        Assert.Equal(ResponsibleUnassigned, planned[monday.AddDays(4)].Status.Entry!.Responsible.Kind);
        Assert.Equal("Grandma Lis", planned[monday.AddDays(12)].Status.Entry!.Responsible.HostName);
        Assert.Equal("", planned[monday.AddDays(12)].Status.Entry!.Responsible.ContactInfo);
        Assert.All(planned.Values, n => Assert.Equal(NightSource.Pattern, n.Status.Source));
        Assert.All(planned.Values, n => Assert.Null(n.Status.Scheduled));
        Assert.All(nights.Where(n => !planned.ContainsKey(n.Date)), n => Assert.Equal(KindUnplanned, n.Status.Kind));
    }

    [Fact]
    public async Task Without_an_effective_date_the_schedule_applies_from_the_childs_today()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        var schedule = await ReplacePatternAsync(fixture, token, child.Id, effectiveFrom: null, 1, FutureMonday(),
            Day(0, DayOfWeek.Monday, Guardian(guardianId)));

        var version = Assert.Single(schedule.Patterns);
        Assert.Equal(schedule.Today, version.EffectiveFrom);
        Assert.Equal(NightPatternStatus.Current, version.Status);
    }

    [Fact]
    public async Task A_schedule_starting_in_the_past_is_rejected()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var yesterdayOrEarlier = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);

        await ReplacePatternExpectingAsync(fixture, token, child.Id, 400, new
        {
            EffectiveFrom = yesterdayOrEarlier,
            CycleWeeks = 1,
            AnchorMonday = FutureMonday(),
            Days = new[] { Day(0, DayOfWeek.Monday, Guardian(guardianId)) },
        });
    }

    [Fact]
    public async Task A_new_version_leaves_the_nights_before_it_on_the_old_schedule()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();

        await ReplacePatternAsync(fixture, token, child.Id, monday, 1, monday, Day(0, DayOfWeek.Monday, Guardian(guardianId)));
        var schedule = await ReplacePatternAsync(fixture, token, child.Id, monday.AddDays(7), 1, monday, Day(0, DayOfWeek.Monday, NoteOnly("New rhythm")));

        Assert.Equal([monday, monday.AddDays(7)], schedule.Patterns.Select(p => p.EffectiveFrom));

        var nights = await ListNightsAsync(fixture, token, child.Id, monday, monday.AddDays(14));

        Assert.Equal(guardianId, nights.Single(n => n.Date == monday).Status.Entry!.Responsible.GuardianId);
        Assert.Equal("New rhythm", nights.Single(n => n.Date == monday.AddDays(7)).Status.Entry!.Note);
        Assert.Equal("New rhythm", nights.Single(n => n.Date == monday.AddDays(14)).Status.Entry!.Note);
    }

    [Fact]
    public async Task Saving_from_an_earlier_date_replaces_an_upcoming_version()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();

        await ReplacePatternAsync(fixture, token, child.Id, monday.AddDays(14), 1, monday, Day(0, DayOfWeek.Monday, NoteOnly("Later")));
        var schedule = await ReplacePatternAsync(fixture, token, child.Id, monday, 1, monday, Day(0, DayOfWeek.Monday, Guardian(guardianId)));

        var version = Assert.Single(schedule.Patterns);
        Assert.Equal(monday, version.EffectiveFrom);

        var nights = await ListNightsAsync(fixture, token, child.Id, monday.AddDays(14), monday.AddDays(14));
        Assert.Equal(guardianId, Assert.Single(nights).Status.Entry!.Responsible.GuardianId);
    }

    [Fact]
    public async Task Saving_the_same_version_appends_no_event()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();
        await ReplacePatternAsync(fixture, token, child.Id, monday, 1, monday, Day(0, DayOfWeek.Monday, Guardian(guardianId)));

        var store = fixture.Host.Services.GetRequiredService<INightScheduleEventStore>();
        var id = NightScheduleId.ForChild(new UserId(child.Id));
        var before = (await store.ReadAsync(id, CancellationToken.None)).Count;

        await ReplacePatternAsync(fixture, token, child.Id, monday, 1, monday, Day(0, DayOfWeek.Monday, Guardian(guardianId)));

        Assert.Equal(before, (await store.ReadAsync(id, CancellationToken.None)).Count);
    }

    [Theory]
    [InlineData(5, 0, "Monday", "ok")]
    [InlineData(2, 2, "Monday", "ok")]
    [InlineData(1, 0, "Funday", "ok")]
    [InlineData(1, 0, "Monday", "")]
    [InlineData(1, 0, "Monday", "61")]
    [InlineData(1, 0, "Monday", "sleepover-without-host")]
    public async Task Structurally_invalid_schedules_are_rejected(int cycleWeeks, int week, string day, string note)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        object entry = note switch
        {
            "61" => NoteOnly(new string('x', 61)),
            "sleepover-without-host" => Sleepover("  "),
            _ => NoteOnly(note),
        };

        await ReplacePatternExpectingAsync(fixture, token, child.Id, 400, new
        {
            CycleWeeks = cycleWeeks,
            AnchorMonday = FutureMonday(),
            Days = new[] { new { Week = week, Day = day, Entry = entry } },
        });
    }

    [Theory]
    [InlineData("61", "days[0].entry.note")]
    [InlineData("", "days[0].entry.responsible")]
    [InlineData("sleepover-without-host", "days[0].entry.responsible")]
    public async Task An_invalid_entry_is_reported_on_its_field(string note, string expectedKey)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        object entry = note switch
        {
            "61" => NoteOnly(new string('x', 61)),
            "sleepover-without-host" => Sleepover("  "),
            _ => NoteOnly(note),
        };

        var response = await ReplacePatternExpectingAsync(fixture, token, child.Id, 400, new
        {
            CycleWeeks = 1,
            AnchorMonday = FutureMonday(),
            Days = new[] { Day(0, DayOfWeek.Monday, entry) },
        });

        Assert.Contains(expectedKey, response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    [Fact]
    public async Task An_anchor_that_is_not_a_monday_and_duplicate_days_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await ReplacePatternExpectingAsync(fixture, token, child.Id, 400, new
        {
            CycleWeeks = 1,
            AnchorMonday = FutureMonday().AddDays(1),
            Days = Array.Empty<object>(),
        });

        await ReplacePatternExpectingAsync(fixture, token, child.Id, 400, new
        {
            CycleWeeks = 1,
            AnchorMonday = FutureMonday(),
            Days = new[] { Day(0, DayOfWeek.Monday, NoteOnly("a")), Day(0, DayOfWeek.Monday, NoteOnly("b")) },
        });
    }

    [Fact]
    public async Task A_guardian_who_is_not_a_guardian_of_the_child_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var (_, _, strangerId) = await fixture.CreateAuthenticatedUserAsync();

        await ReplacePatternExpectingAsync(fixture, token, child.Id, 400, new
        {
            CycleWeeks = 1,
            AnchorMonday = FutureMonday(),
            Days = new[] { Day(0, DayOfWeek.Monday, Guardian(strangerId)) },
        });
    }

    [Fact]
    public async Task An_unknown_or_archived_babysitter_is_rejected_and_an_active_one_resolves_by_name()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var maja = await BabysitterTestHelpers.AddAsync(fixture, token, "Maja");
        var freja = await BabysitterTestHelpers.AddAsync(fixture, token, "Freja");
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, freja.Id);
        var monday = FutureMonday();

        foreach (var babysitterId in new[] { Guid.CreateVersion7(), freja.Id })
        {
            await ReplacePatternExpectingAsync(fixture, token, child.Id, 400, new
            {
                CycleWeeks = 1,
                AnchorMonday = monday,
                Days = new[] { Day(0, DayOfWeek.Monday, Babysitter(guardianId, babysitterId)) },
            });
        }

        var schedule = await ReplacePatternAsync(fixture, token, child.Id, monday, 1, monday, Day(0, DayOfWeek.Monday, Babysitter(guardianId, maja.Id)));

        Assert.Equal("Maja", Assert.Single(Assert.Single(schedule.Patterns).Days).Entry.Responsible.Name);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var body = new { CycleWeeks = 1, AnchorMonday = FutureMonday(), Days = new[] { Day(0, DayOfWeek.Monday, Guardian(guardianId)) } };

        await ReplacePatternExpectingAsync(fixture, childToken, child.Id, 403, body);
        await ReplacePatternExpectingAsync(fixture, strangerToken, child.Id, 404, body);
    }
}
