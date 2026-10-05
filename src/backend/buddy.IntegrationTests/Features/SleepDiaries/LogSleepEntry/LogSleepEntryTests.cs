using buddy.Features.SleepDiaries;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.SleepDiaries.SleepDiaryTestHelpers;

namespace buddy.IntegrationTests.Features.SleepDiaries.LogSleepEntry;

[Collection(BuddyApiCollection.Name)]
public sealed class LogSleepEntryTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("LogSleepEntry")]
    public async Task A_guardian_logs_a_full_night_and_reads_it_back()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        var logged = await LogAsync(fixture, token, child.Id, Monday, FullNight());

        Assert.NotNull(logged);
        Assert.Equal(Monday, logged.Date);
        Assert.False(logged.IsWeekend);
        Assert.Equal(new TimeOnly(19, 0), logged.RoutineStartTime);
        Assert.Equal(new TimeOnly(19, 30), logged.RitualStartTime);
        Assert.Equal(new TimeOnly(20, 10), logged.RitualEndTime);
        Assert.Equal(new TimeOnly(20, 15), logged.BedTime);
        Assert.Equal(new TimeOnly(20, 45), logged.FellAsleepTime);
        Assert.Equal(new SleepIntervalDto(new TimeOnly(3, 30), 30), Assert.Single(logged.NightWakeUps));
        Assert.Equal(new TimeOnly(6, 30), logged.MorningWakeTime);
        Assert.True(logged.IsTired);
        Assert.Equal(new SleepIntervalDto(new TimeOnly(13, 0), 45), Assert.Single(logged.Naps));
        Assert.Equal(555, logged.TotalSleepMinutes);
        Assert.Equal("Cried for 20 minutes before settling", logged.Remarks);
        Assert.Equal(guardianId, logged.LoggedBy);

        var listed = await ListAsync(fixture, token, child.Id, Monday, Monday);

        Assert.Equivalent(logged, Assert.Single(listed!.Entries), strict: true);
    }

    [Fact]
    public async Task A_partial_entry_is_allowed_and_the_rest_stays_blank()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        var logged = await LogAsync(fixture, token, child.Id, Saturday, new { BedTime = "20:00", MorningWakeTime = "07:00" });

        Assert.NotNull(logged);
        Assert.True(logged.IsWeekend);
        Assert.Equal(new TimeOnly(20, 0), logged.BedTime);
        Assert.Equal(new TimeOnly(7, 0), logged.MorningWakeTime);
        Assert.Null(logged.RoutineStartTime);
        Assert.Null(logged.FellAsleepTime);
        Assert.Null(logged.TotalSleepMinutes);
        Assert.Empty(logged.NightWakeUps);
        Assert.Empty(logged.Naps);
        Assert.False(logged.IsTired);
        Assert.Equal("", logged.Remarks);
    }

    [Fact]
    public async Task Re_logging_a_day_overwrites_the_whole_entry()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await LogAsync(fixture, token, child.Id, Monday, FullNight());
        await LogAsync(fixture, token, child.Id, Monday, new { BedTime = "21:00" });

        var entry = Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday))!.Entries);

        Assert.Equal(new TimeOnly(21, 0), entry.BedTime);
        Assert.Null(entry.RoutineStartTime);
        Assert.Empty(entry.NightWakeUps);
        Assert.False(entry.IsTired);

        var events = await ReadEventsAsync(child.Id);
        var relogged = Assert.IsType<SleepEntryLogged>(events.Last().Value);
        Assert.NotNull(relogged.Before);
        Assert.Equal(new TimeOnly(20, 15), relogged.Before.BedTime);
    }

    [Fact]
    public async Task Saving_an_identical_entry_again_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await LogAsync(fixture, token, child.Id, Monday, FullNight());
        var countAfterFirst = (await ReadEventsAsync(child.Id)).Count;

        await LogAsync(fixture, token, child.Id, Monday, FullNight());

        Assert.Equal(countAfterFirst, (await ReadEventsAsync(child.Id)).Count);
    }

    [Fact]
    public async Task The_first_log_starts_the_diary_lazily()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        Assert.Empty(await ReadEventsAsync(child.Id));

        await LogAsync(fixture, token, child.Id, Monday, FullNight());

        var events = await ReadEventsAsync(child.Id);
        Assert.Collection(
            events,
            e => Assert.IsType<SleepDiaryStarted>(e.Value),
            e => Assert.IsType<SleepEntryLogged>(e.Value));
    }

    [Fact]
    public async Task A_past_or_future_date_is_allowed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await LogAsync(fixture, token, child.Id, today.AddDays(-60), new { BedTime = "20:00" });
        await LogAsync(fixture, token, child.Id, today.AddDays(3), new { BedTime = "20:00" });
    }

    [Fact]
    public async Task Two_guardians_logging_the_same_day_last_write_wins_and_both_stay_in_history()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);

        await LogAsync(fixture, family.FirstToken, family.Child.Id, Monday, new { BedTime = "20:00" });
        await LogAsync(fixture, family.SecondToken, family.Child.Id, Monday, new { BedTime = "20:30" });

        var entry = Assert.Single((await ListAsync(fixture, family.FirstToken, family.Child.Id, Monday, Monday))!.Entries);
        Assert.Equal(new TimeOnly(20, 30), entry.BedTime);
        Assert.Equal(family.SecondId, entry.LoggedBy);

        Assert.Equal(2, (await ReadEventsAsync(family.Child.Id)).Count(e => e.Value is SleepEntryLogged));
    }

    [Theory]
    [InlineData("{\"nightWakeUps\":[{\"startTime\":\"03:00\",\"durationMinutes\":0}]}")]
    [InlineData("{\"naps\":[{\"startTime\":\"13:00\",\"durationMinutes\":721}]}")]
    [InlineData("{\"totalSleepMinutes\":-1}")]
    [InlineData("{\"totalSleepMinutes\":1441}")]
    public async Task Out_of_range_durations_are_rejected(string json)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await LogAsync(fixture, token, child.Id, Monday, System.Text.Json.JsonDocument.Parse(json).RootElement, expectedStatus: 400);
    }

    [Fact]
    public async Task Too_many_wake_ups_or_overlong_remarks_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var wakeUps = Enumerable.Range(0, LogSleepEntryValidator.MaxIntervals + 1).Select(_ => new { StartTime = "02:00", DurationMinutes = 5 }).ToArray();

        await LogAsync(fixture, token, child.Id, Monday, new { NightWakeUps = wakeUps }, expectedStatus: 400);
        await LogAsync(fixture, token, child.Id, Monday, new { Remarks = new string('x', LogSleepEntryValidator.MaxRemarksLength + 1) }, expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await LogAsync(fixture, childToken, child.Id, Monday, FullNight(), expectedStatus: 404);
    }

    [Fact]
    public async Task An_unrelated_user_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await LogAsync(fixture, strangerToken, child.Id, Monday, FullNight(), expectedStatus: 404);
    }

    [Fact]
    public async Task A_guardian_whose_link_is_revoked_gets_not_found_and_the_entries_survive()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await LogAsync(fixture, family.SecondToken, family.Child.Id, Monday, FullNight());

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await LogAsync(fixture, family.SecondToken, family.Child.Id, Monday, FullNight(), expectedStatus: 404);
        await ListAsync(fixture, family.SecondToken, family.Child.Id, Monday, Monday, expectedStatus: 404);
        Assert.Single((await ListAsync(fixture, family.FirstToken, family.Child.Id, Monday, Monday))!.Entries);
    }

    private async Task<IReadOnlyCollection<SleepDiaryEvent>> ReadEventsAsync(Guid childId)
    {
        var store = fixture.Host.Services.GetRequiredService<ISleepDiaryEventStore>();

        return await store.ReadAsync(SleepDiaryId.ForChild(new UserId(childId)), CancellationToken.None);
    }
}
