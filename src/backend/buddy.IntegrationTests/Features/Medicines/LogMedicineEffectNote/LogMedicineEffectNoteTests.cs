using buddy.Features.Medicines;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Feelings;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Medicines.MedicineEffectNoteTestHelpers;

namespace buddy.IntegrationTests.Features.Medicines.LogMedicineEffectNote;

[Collection(BuddyApiCollection.Name)]
public sealed class LogMedicineEffectNoteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("LogMedicineEffectNote")]
    public async Task A_guardian_logs_a_note_and_it_records_the_medicine_and_dosage_of_that_day()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Ritalin", Dosage: "10 mg", Times: [new TimeOnly(7, 30)], StartDate: Monday.AddDays(-7)));

        var logged = await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());

        Assert.NotNull(logged);
        Assert.Equal(Monday, logged.Date);
        Assert.Equal(new EffectRating(EffectArea.Focus, EffectLevel.Good), Assert.Single(logged.Effects));
        Assert.Equal(new SideEffectRating(SideEffect.ReducedAppetite, SideEffectSeverity.Moderate), Assert.Single(logged.SideEffects));
        Assert.Equal(new TimeOnly(15, 30), logged.WoreOffAt);
        Assert.Equal("Great in maths, skipped lunch", logged.Note);
        Assert.Equal(guardianId, logged.LoggedBy);

        var medicine = Assert.Single(logged.Regimen);
        Assert.Equal(schedule!.Id, medicine.MedicineId);
        Assert.Equal("Ritalin", medicine.Name);
        Assert.Equal("10 mg", medicine.Dosage);
        Assert.Equal(new RegimenDoseDto(new TimeOnly(7, 30), DoseStatus.Pending), Assert.Single(medicine.Doses));

        var listed = await ListAsync(fixture, token, child.Id, Monday, Monday);
        Assert.Equivalent(logged, Assert.Single(listed!.Notes), strict: true);
    }

    [Fact]
    public async Task A_day_with_no_active_medicine_is_a_baseline_with_an_empty_regimen()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(StartDate: Monday.AddDays(1)));

        var logged = await LogAsync(fixture, token, child.Id, Monday, new { Effects = new[] { new { Area = "Calm", Level = "Poor" } } });

        Assert.Empty(logged!.Regimen);
    }

    [Fact]
    public async Task Editing_a_day_keeps_the_dosage_it_was_first_logged_with_and_clearing_resolves_it_again()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Ritalin", Dosage: "10 mg", StartDate: Monday.AddDays(-7)));

        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());
        await UpdateDosageAsync(fixture, token, child.Id, schedule!.Id, "Ritalin", "15 mg");

        var edited = await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite("Typo fixed"));
        Assert.Equal("10 mg", Assert.Single(edited!.Regimen).Dosage);

        await ClearAsync(fixture, token, child.Id, Monday);
        var relogged = await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());
        Assert.Equal("15 mg", Assert.Single(relogged!.Regimen).Dosage);
    }

    [Fact]
    public async Task A_stopped_medicine_is_left_out_of_a_new_note()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var stopped = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Ritalin", StartDate: Monday.AddDays(-7)));
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Melatonin", StartDate: Monday.AddDays(-7)));
        await StopAsync(fixture, token, child.Id, stopped!.Id);

        var logged = await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());

        Assert.Equal("Melatonin", Assert.Single(logged!.Regimen).Name);
    }

    [Fact]
    public async Task The_first_note_starts_the_log_and_an_identical_re_save_in_another_order_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        Assert.Empty(await ReadEventsAsync(child.Id));

        var body = new
        {
            Effects = new[] { new { Area = "Mood", Level = "Okay" }, new { Area = "Focus", Level = "Good" } },
        };
        var reordered = new
        {
            Effects = new[] { new { Area = "Focus", Level = "Good" }, new { Area = "Mood", Level = "Okay" } },
        };

        var logged = await LogAsync(fixture, token, child.Id, Monday, body);
        Assert.Equal([EffectArea.Focus, EffectArea.Mood], logged!.Effects.Select(e => e.Area));

        await LogAsync(fixture, token, child.Id, Monday, reordered);

        Assert.Collection(
            await ReadEventsAsync(child.Id),
            e => Assert.IsType<MedicineEffectLogStarted>(e.Value),
            e => Assert.IsType<MedicineEffectNoteLogged>(e.Value));
    }

    [Fact]
    public async Task Re_logging_a_day_overwrites_it_and_keeps_the_previous_version_in_history()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);

        await LogAsync(fixture, family.FirstToken, family.Child.Id, Monday, GoodFocusPoorAppetite());
        await LogAsync(fixture, family.SecondToken, family.Child.Id, Monday, new { Note = "Slow to fall asleep" });

        var note = Assert.Single((await ListAsync(fixture, family.FirstToken, family.Child.Id, Monday, Monday))!.Notes);
        Assert.Empty(note.Effects);
        Assert.Null(note.WoreOffAt);
        Assert.Equal("Slow to fall asleep", note.Note);
        Assert.Equal(family.SecondId, note.LoggedBy);

        var relogged = Assert.IsType<MedicineEffectNoteLogged>((await ReadEventsAsync(family.Child.Id)).Last().Value);
        Assert.Equal("Great in maths, skipped lunch", relogged.Before!.Note);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"note\":\"   \"}")]
    [InlineData("{\"effects\":[{\"area\":\"Focus\",\"level\":\"Good\"},{\"area\":\"Focus\",\"level\":\"Poor\"}]}")]
    [InlineData("{\"sideEffects\":[{\"kind\":\"Tics\",\"severity\":\"Mild\"},{\"kind\":\"Tics\",\"severity\":\"Severe\"}]}")]
    [InlineData("{\"effects\":[{\"area\":\"Sleepiness\",\"level\":\"Good\"}]}")]
    [InlineData("{\"effects\":[{\"area\":99,\"level\":\"Good\"}]}")]
    [InlineData("{\"sideEffects\":[{\"kind\":\"Tics\",\"severity\":7}]}")]
    [InlineData("{\"effects\":[null]}")]
    [InlineData("{\"sideEffects\":[null]}")]
    public async Task An_invalid_note_is_rejected(string json)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Text(json).ToUrl($"/medicines/children/{child.Id}/effect-notes/{Monday:yyyy-MM-dd}");
            _.WithRequestHeader("Content-Type", "application/json");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_note_longer_than_500_characters_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await LogAsync(fixture, token, child.Id, Monday, new { Note = new string('x', 500) });
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Note = new string('x', 501) }).ToUrl($"/medicines/children/{child.Id}/effect-notes/{Monday:yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });

        MedicineTestHelpers.AssertValidationError(response, "Note");
    }

    [Fact]
    public async Task A_day_after_today_on_the_childs_clock_is_rejected_but_today_and_the_past_are_not()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);

        // Two days ahead is after today in every time zone; a year back has no lower bound.
        await LogAsync(fixture, token, child.Id, utcToday.AddDays(2), new { Note = "Tomorrow" }, expectedStatus: 400);
        await LogAsync(fixture, token, child.Id, utcToday.AddDays(-1), new { Note = "Yesterday" });
        await LogAsync(fixture, token, child.Id, utcToday.AddDays(-365), new { Note = "Last year" });
    }

    [Fact]
    public async Task Today_comes_from_the_childs_own_time_zone()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        // Two zones 23 hours apart: at least one of them is on another date than UTC at any moment.
        foreach (var zone in new[] { "Pacific/Kiritimati", "Pacific/Pago_Pago" })
        {
            await FeelingsTestHelpers.SetChildTimeZoneAsync(fixture, token, child.Id, zone);
            var (childToday, _) = FeelingsTestHelpers.LocalNow(zone);

            await LogAsync(fixture, token, child.Id, childToday, new { Note = zone });
            await LogAsync(fixture, token, child.Id, childToday.AddDays(1), new { Note = zone }, expectedStatus: 400);
        }
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await LogAsync(fixture, childToken, child.Id, Monday, GoodFocusPoorAppetite(), expectedStatus: 403);
        await LogAsync(fixture, strangerToken, child.Id, Monday, GoodFocusPoorAppetite(), expectedStatus: 404);

        Assert.Empty(await ReadEventsAsync(child.Id));
    }

    [Fact]
    public async Task A_guardian_whose_link_is_revoked_gets_not_found_and_the_notes_survive()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await LogAsync(fixture, family.SecondToken, family.Child.Id, Monday, GoodFocusPoorAppetite());

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await LogAsync(fixture, family.SecondToken, family.Child.Id, Monday, GoodFocusPoorAppetite(), expectedStatus: 404);
        await ListAsync(fixture, family.SecondToken, family.Child.Id, Monday, Monday, expectedStatus: 404);
        Assert.Single((await ListAsync(fixture, family.FirstToken, family.Child.Id, Monday, Monday))!.Notes);
    }

    private async Task<IReadOnlyCollection<MedicineEffectLogEvent>> ReadEventsAsync(Guid childId)
    {
        var store = fixture.Host.Services.GetRequiredService<IMedicineEffectLogEventStore>();

        return await store.ReadAsync(MedicineEffectLogId.ForChild(new UserId(childId)), CancellationToken.None);
    }
}
