using Alba;

using buddy.Common;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

using Xunit;

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

    // Creates a group owned by the guardian (Owner holds Manage on the default medicine policy) and
    // shares the child's medicine with it, so the guardian can drive the /medicines/groups/... routes.
    public static async Task<Guid> ShareWithNewGroupAsync(BuddyApiFixture fixture, string guardianToken, Guid childId)
    {
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Co-parents");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/medicines/children/{childId}/group-share/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        return groupId;
    }

    public static void AssertValidationError(IScenarioResult response, string field)
    {
        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains(field, error.Details.Keys);
    }
}
