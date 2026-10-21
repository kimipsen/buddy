using buddy.Features.Medicines;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Medicines.MedicineEffectNoteTestHelpers;

namespace buddy.IntegrationTests.Features.Medicines.SetMedicineEffectInclusion;

// docs/backend/analysis/medicine-effect-inclusion.md: a guardian leaves a medicine out of the
// effect summaries; notes keep their full regimen and get the flag live.
[Collection(BuddyApiCollection.Name)]
public sealed class SetMedicineEffectInclusionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("SetMedicineEffectInclusion")]
    public async Task Leaving_a_medicine_out_flags_it_on_the_schedule_and_on_notes_written_before()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id,
            new CreateMedicineScheduleOptions(Name: "Vitamin D", Dosage: "1 tablet", Times: [new TimeOnly(7, 30)], StartDate: Monday));
        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());

        await SetEffectInclusionAsync(fixture, token, child.Id, schedule!.Id, included: false);

        var listed = Assert.Single((await MedicineTestHelpers.ListMedicineSchedulesAsync(fixture, token, child.Id))!);
        Assert.True(listed.IsExcludedFromEffects);
        var regimen = Assert.Single(Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday))!.Notes).Regimen);
        Assert.Equal("Vitamin D", regimen.Name);
        Assert.True(regimen.IsExcludedFromEffects);

        await SetEffectInclusionAsync(fixture, token, child.Id, schedule.Id, included: true);

        Assert.False(Assert.Single((await MedicineTestHelpers.ListMedicineSchedulesAsync(fixture, token, child.Id))!).IsExcludedFromEffects);
    }

    [Fact]
    public async Task Setting_the_value_it_already_has_appends_no_event()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id);
        var store = fixture.Host.Services.GetRequiredService<IMedicineEventStore>();
        var id = new MedicineId(schedule!.Id);

        await SetEffectInclusionAsync(fixture, token, child.Id, schedule.Id, included: true);
        Assert.Single(await store.ReadAsync(id, CancellationToken.None));

        await SetEffectInclusionAsync(fixture, token, child.Id, schedule.Id, included: false);
        await SetEffectInclusionAsync(fixture, token, child.Id, schedule.Id, included: false);
        Assert.Equal(2, (await store.ReadAsync(id, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_stopped_schedule_can_still_be_left_out()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id);
        await StopAsync(fixture, token, child.Id, schedule!.Id);

        await SetEffectInclusionAsync(fixture, token, child.Id, schedule.Id, included: false);

        var listed = Assert.Single((await MedicineTestHelpers.ListMedicineSchedulesAsync(fixture, token, child.Id))!);
        Assert.True(listed.IsStopped);
        Assert.True(listed.IsExcludedFromEffects);
    }

    [Fact]
    public async Task The_child_cannot_change_it()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id);

        await SetEffectInclusionAsync(fixture, childToken, child.Id, schedule!.Id, included: false, expectedStatus: 403);
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var schedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id);

        await SetEffectInclusionAsync(fixture, strangerToken, child.Id, schedule!.Id, included: false, expectedStatus: 404);
    }

    [Fact]
    public async Task Another_childs_medicine_or_an_unknown_id_is_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Sam");
        var siblingsSchedule = await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, sibling.Id);

        await SetEffectInclusionAsync(fixture, token, child.Id, siblingsSchedule!.Id, included: false, expectedStatus: 404);
        await SetEffectInclusionAsync(fixture, token, child.Id, Guid.CreateVersion7(), included: false, expectedStatus: 404);
    }
}
