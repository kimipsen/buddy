using Alba;

using buddy.IntegrationTests.Features.Feelings;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Features.SleepDiaries;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.VisitSummaries.VisitSummaryTestHelpers;

namespace buddy.IntegrationTests.Features.VisitSummaries.Report;

// The report is read live from the sleep diary, medicines and feelings for the summary's period
// (docs/backend/analysis/doctor-visit-summary.md, Questions 3-6).
[Collection(BuddyApiCollection.Name)]
public sealed class VisitSummaryReportTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private IAlbaHost? _feelingsOff;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_feelingsOff is not null)
        {
            await _feelingsOff.DisposeAsync();
        }
    }

    [Fact]
    [CoversEndpoint("GetVisitSummary")]
    public async Task The_report_holds_the_periods_sleep_medicine_and_feelings()
    {
        var token = await fixture.GetAccessTokenAsync(await fixture.CreateUserAsync("Sara", "Holm"));
        await fixture.GetUserIdAsync(token);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil", "Holm");
        var medicine = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Concerta", Dosage: "18 mg", Times: [new TimeOnly(7, 15)], StartDate: From.AddDays(-30)));
        await MedicineEffectNoteTestHelpers.MarkTakenAsync(fixture, token, child.Id, medicine!.Id, From, new TimeOnly(7, 15));
        await MedicineEffectNoteTestHelpers.LogAsync(fixture, token, child.Id, From, MedicineEffectNoteTestHelpers.GoodFocusPoorAppetite("Skipped lunch"));
        await SleepDiaryTestHelpers.LogAsync(fixture, token, child.Id, From, SleepDiaryTestHelpers.FullNight());
        await SleepDiaryTestHelpers.LogAsync(fixture, token, child.Id, From.AddDays(-1), SleepDiaryTestHelpers.FullNight());
        await FeelingsTestHelpers.LogAsync(fixture, token, child.Id, new { Zone = "Red", Note = "Meltdown after the bus", Date = From, Time = "16:10" });
        var summary = await CreateAsync(fixture, token, child.Id);

        var result = await GetAsync(fixture, token, child.Id, summary.Id);

        var report = result!.Report;
        Assert.Equal(summary.Id, result.Summary.Id);
        Assert.Equal(("Emil", "Holm"), (report.ChildGivenName, report.ChildFamilyName));
        Assert.Equal("Sara Holm", report.PreparedBy);
        Assert.Equal((From, To), (report.From, report.To));
        Assert.Equal("Afternoons are harder since the dose change.", report.Note);

        Assert.Equal(From, Assert.Single(report.Sleep!.Entries).Date);

        Assert.Equal("Concerta", Assert.Single(report.Medicine!.Schedules).Name);
        Assert.Equal(28, report.Medicine.Doses.Count);
        Assert.Equal("Taken", report.Medicine.Doses[0].Status);
        Assert.Equal("Pending", report.Medicine.Doses[1].Status);
        Assert.Equal("Skipped lunch", Assert.Single(report.Medicine.EffectNotes).Note);

        var checkIn = Assert.Single(report.Feelings!.CheckIns);
        Assert.Equal(("Red", "Meltdown after the bus"), (checkIn.Zone, checkIn.Note));
    }

    [Fact]
    public async Task A_section_left_out_of_the_summary_is_null_and_the_others_stay()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id, Body(sections: ["Sleep"]));

        var report = (await GetAsync(fixture, token, child.Id, summary.Id))!.Report;

        Assert.NotNull(report.Sleep);
        Assert.Empty(report.Sleep.Entries);
        Assert.Null(report.Medicine);
        Assert.Null(report.Feelings);
    }

    [Fact]
    public async Task A_note_the_child_wrote_themself_is_not_in_the_report()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        await FeelingsTestHelpers.CheckInAsync(fixture, childToken, new { Zone = "Blue", Tags = new[] { "Tired" }, Note = "Secret note for mum" });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = await CreateAsync(fixture, token, child.Id, Body(from: today.AddDays(-7), to: today.AddDays(1), sections: ["Feelings"]));

        var checkIn = Assert.Single((await GetAsync(fixture, token, child.Id, summary.Id))!.Report.Feelings!.CheckIns);

        Assert.True(checkIn.IsSelfReport);
        Assert.Equal("Blue", checkIn.Zone);
        Assert.Equal("", checkIn.Note);
    }

    [Fact]
    public async Task A_medicine_stopped_during_the_period_keeps_only_its_recorded_doses()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var stopped = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Concerta", Dosage: "18 mg", Times: [new TimeOnly(7, 15)], StartDate: From.AddDays(-30)));
        await MedicineEffectNoteTestHelpers.MarkTakenAsync(fixture, token, child.Id, stopped!.Id, From, new TimeOnly(7, 15));
        await MedicineEffectNoteTestHelpers.MarkTakenAsync(fixture, token, child.Id, stopped.Id, From.AddDays(1), new TimeOnly(7, 15));
        await MedicineEffectNoteTestHelpers.StopAsync(fixture, token, child.Id, stopped.Id);
        var summary = await CreateAsync(fixture, token, child.Id, Body(sections: ["Medicine"]));

        var medicine = (await GetAsync(fixture, token, child.Id, summary.Id))!.Report.Medicine!;

        Assert.True(Assert.Single(medicine.Schedules).IsStopped);
        Assert.Equal([From, From.AddDays(1)], medicine.Doses.Select(d => d.Date));
        Assert.All(medicine.Doses, d => Assert.Equal("Taken", d.Status));
    }

    [Fact]
    public async Task A_section_whose_feature_is_switched_off_is_left_out()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);
        _feelingsOff = await fixture.CreateHostAsync(new Dictionary<string, string?> { ["Features:Feelings"] = "false" });

        var report = (await GetAsync(_feelingsOff, token, child.Id, summary.Id))!.Report;

        Assert.NotNull(report.Sleep);
        Assert.NotNull(report.Medicine);
        Assert.Null(report.Feelings);
    }
}
