using Alba;

using buddy.Features.Medicines;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Medicines;

// Response shapes matching MedicineEffectNotesResponse / MedicineEffectNoteResponse.
internal sealed record MedicineEffectNotesDto(IReadOnlyList<MedicineEffectNoteDto> Notes);

internal sealed record MedicineEffectNoteDto(
    DateOnly Date,
    IReadOnlyList<EffectRating> Effects,
    IReadOnlyList<SideEffectRating> SideEffects,
    TimeOnly? WoreOffAt,
    string Note,
    IReadOnlyList<RegimenMedicineDto> Regimen,
    Guid LoggedBy);

internal sealed record RegimenMedicineDto(Guid MedicineId, string Name, string Dosage, IReadOnlyList<RegimenDoseDto> Doses, bool IsExcludedFromEffects);

internal sealed record RegimenDoseDto(TimeOnly Time, DoseStatus Status);

internal static class MedicineEffectNoteTestHelpers
{
    // A fixed past date: never after the child's today, whatever the time zone.
    public static readonly DateOnly Monday = new(2026, 3, 2);

    public static object GoodFocusPoorAppetite(string note = "Great in maths, skipped lunch") => new
    {
        Effects = new[] { new { Area = "Focus", Level = "Good" } },
        SideEffects = new[] { new { Kind = "ReducedAppetite", Severity = "Moderate" } },
        WoreOffAt = "15:30",
        Note = note
    };

    public static async Task<MedicineEffectNoteDto?> LogAsync(
        BuddyApiFixture fixture, string token, Guid childId, DateOnly date, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl($"/medicines/children/{childId}/effect-notes/{date:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<MedicineEffectNoteDto>() : null;
    }

    public static async Task ClearAsync(BuddyApiFixture fixture, string token, Guid childId, DateOnly date, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/medicines/children/{childId}/effect-notes/{date:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<MedicineEffectNotesDto?> ListAsync(
        BuddyApiFixture fixture, string token, Guid childId, DateOnly from, DateOnly to, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/medicines/children/{childId}/effect-notes?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<MedicineEffectNotesDto>() : null;
    }

    public static async Task UpdateDosageAsync(BuddyApiFixture fixture, string token, Guid childId, Guid medicineId, string name, string dosage) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = name, Dosage = dosage, Icon = "pill", Color = "#ff8800" })
                .ToUrl($"/medicines/children/{childId}/schedules/{medicineId}/details");
            _.StatusCodeShouldBeOk();
        });

    public static async Task StopAsync(BuddyApiFixture fixture, string token, Guid childId, Guid medicineId) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/medicines/children/{childId}/schedules/{medicineId}");
            _.StatusCodeShouldBe(204);
        });

    public static async Task SetEffectInclusionAsync(
        BuddyApiFixture fixture, string token, Guid childId, Guid medicineId, bool included, int expectedStatus = 204) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Included = included }).ToUrl($"/medicines/children/{childId}/schedules/{medicineId}/effect-inclusion");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task MarkTakenAsync(BuddyApiFixture fixture, string token, Guid childId, Guid medicineId, DateOnly date, TimeOnly time) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Status = "Taken" }).ToUrl($"/medicines/children/{childId}/doses/{medicineId}")
                .QueryString("date", $"{date:yyyy-MM-dd}")
                .QueryString("time", $"{time:HH:mm:ss}");
            _.StatusCodeShouldBeOk();
        });
}
