using Alba;

using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.Medicines;

internal sealed record CreateMedicineScheduleOptions(
    string Name = "Amoxicillin",
    string Dosage = "5 ml",
    IReadOnlyList<TimeOnly>? Times = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null);

internal static class MedicineTestHelpers
{
    public static async Task<MedicineScheduleDto?> CreateMedicineScheduleAsync(
        BuddyApiFixture fixture, string guardianToken, Guid childId, CreateMedicineScheduleOptions? options = null, int expectedStatus = 200)
    {
        options ??= new CreateMedicineScheduleOptions();
        var times = options.Times ?? [new TimeOnly(8, 0), new TimeOnly(20, 0)];
        var start = options.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new
            {
                options.Name,
                options.Dosage,
                Icon = "pill",
                Color = "#ff8800",
                Times = times,
                StartDate = start,
                options.EndDate
            }).ToUrl($"/medicines/children/{childId}/schedules");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<MedicineScheduleDto>() : null;
    }
}
