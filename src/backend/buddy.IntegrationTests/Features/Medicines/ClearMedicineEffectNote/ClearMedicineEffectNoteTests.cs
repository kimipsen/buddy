using buddy.Features.Medicines;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Medicines.MedicineEffectNoteTestHelpers;

namespace buddy.IntegrationTests.Features.Medicines.ClearMedicineEffectNote;

[Collection(BuddyApiCollection.Name)]
public sealed class ClearMedicineEffectNoteTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ClearMedicineEffectNote")]
    public async Task A_guardian_clears_one_day_and_the_other_days_stay()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());
        await LogAsync(fixture, token, child.Id, Monday.AddDays(1), GoodFocusPoorAppetite());

        await ClearAsync(fixture, token, child.Id, Monday);

        var note = Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday.AddDays(1)))!.Notes);
        Assert.Equal(Monday.AddDays(1), note.Date);

        var cleared = Assert.IsType<MedicineEffectNoteCleared>((await ReadEventsAsync(child.Id)).Last().Value);
        Assert.Equal(guardianId, cleared.ModifiedBy.Value);
        Assert.Equal("Great in maths, skipped lunch", cleared.Before.Note);
    }

    [Fact]
    public async Task Clearing_a_day_without_a_note_is_a_no_op()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await ClearAsync(fixture, token, child.Id, Monday);
        Assert.Empty(await ReadEventsAsync(child.Id));

        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());
        await ClearAsync(fixture, token, child.Id, Monday);
        await ClearAsync(fixture, token, child.Id, Monday);

        Assert.Single(await ReadEventsAsync(child.Id), e => e.Value is MedicineEffectNoteCleared);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_gets_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await LogAsync(fixture, token, child.Id, Monday, GoodFocusPoorAppetite());

        await ClearAsync(fixture, childToken, child.Id, Monday, expectedStatus: 403);
        await ClearAsync(fixture, strangerToken, child.Id, Monday, expectedStatus: 404);

        Assert.Single((await ListAsync(fixture, token, child.Id, Monday, Monday))!.Notes);
    }

    private async Task<IReadOnlyCollection<MedicineEffectLogEvent>> ReadEventsAsync(Guid childId)
    {
        var store = fixture.Host.Services.GetRequiredService<IMedicineEffectLogEventStore>();

        return await store.ReadAsync(MedicineEffectLogId.ForChild(new UserId(childId)), CancellationToken.None);
    }
}
