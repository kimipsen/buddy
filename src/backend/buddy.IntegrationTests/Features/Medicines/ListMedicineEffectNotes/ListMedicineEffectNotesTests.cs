using buddy.Features.Medicines;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.Medicines.MedicineEffectNoteTestHelpers;

namespace buddy.IntegrationTests.Features.Medicines.ListMedicineEffectNotes;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMedicineEffectNotesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMedicineEffectNotes")]
    public async Task A_guardian_lists_the_notes_in_a_range_in_date_order()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        await LogAsync(fixture, token, child.Id, Monday.AddDays(2), new { Note = "Wednesday" });
        await LogAsync(fixture, token, child.Id, Monday, new { Note = "Monday" });
        await LogAsync(fixture, token, child.Id, Monday.AddDays(10), new { Note = "Outside the range" });

        var listed = await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(6));

        Assert.Equal(["Monday", "Wednesday"], listed!.Notes.Select(n => n.Note));
    }

    [Fact]
    public async Task A_child_with_no_notes_lists_an_empty_range()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        Assert.Empty((await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(6)))!.Notes);
    }

    [Fact]
    public async Task Dose_statuses_are_read_live_so_a_dose_marked_after_the_note_shows()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Times: [new TimeOnly(20, 0), new TimeOnly(7, 30)], StartDate: Monday.AddDays(-7)));

        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());
        await MarkTakenAsync(fixture, token, child.Id, schedule!.Id, Monday, new TimeOnly(7, 30));

        var medicine = Assert.Single(Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday))!.Notes).Regimen);
        Assert.Equal(
            [new RegimenDoseDto(new TimeOnly(7, 30), DoseStatus.Taken), new RegimenDoseDto(new TimeOnly(20, 0), DoseStatus.Pending)],
            medicine.Doses);
    }

    [Fact]
    public async Task A_note_keeps_a_medicine_stopped_after_it_was_written()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Ritalin", Times: [new TimeOnly(7, 30)], StartDate: Monday.AddDays(-7)));
        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());
        await MarkTakenAsync(fixture, token, child.Id, schedule!.Id, Monday, new TimeOnly(7, 30));

        await StopAsync(fixture, token, child.Id, schedule.Id);

        var medicine = Assert.Single(Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday))!.Notes).Regimen);
        Assert.Equal("Ritalin", medicine.Name);
        Assert.Equal(DoseStatus.Taken, Assert.Single(medicine.Doses).Status);
    }

    [Fact]
    public async Task A_range_longer_than_92_days_or_reversed_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(ListMedicineEffectNotesHandler.MaxRangeDays));
        await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(ListMedicineEffectNotesHandler.MaxRangeDays + 1), expectedStatus: 400);
        await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(-1), expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await ListAsync(fixture, childToken, child.Id, Monday, Monday, expectedStatus: 403);
        await ListAsync(fixture, strangerToken, child.Id, Monday, Monday, expectedStatus: 404);
    }
}
