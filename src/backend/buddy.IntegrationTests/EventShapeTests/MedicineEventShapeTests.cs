using buddy.Features.Calendars;
using buddy.Features.Medicines;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class MedicineEventShapeTests
{
    private static readonly MedicineId FixedMedicineId = new(Guid.Parse("00000000-0000-0000-0000-000000000050"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<TimeOnly> FixedTimes = [new TimeOnly(8, 0), new TimeOnly(20, 0)];
    private static readonly DateOnly FixedStartDate = new(2025, 6, 1);
    private static readonly DateOnly FixedEndDate = new(2025, 6, 10);

    [Fact]
    public void MedicineScheduleCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MedicineScheduleCreated(
            FixedMedicineId, FixedChildId, FixedGuardianId, "Amoxicillin", "5 ml",
            Icon.New("pill"), Color.New("#ff8800"), FixedTimes, FixedStartDate, FixedEndDate, FixedInstant),
        "Medicines/MedicineScheduleCreated.json");

    [Fact]
    public void MedicineDetailsUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MedicineDetailsUpdated(
            FixedMedicineId,
            new MedicineDetails("Amoxicillin", "5 ml", Icon.New("pill"), Color.New("#ff8800")),
            new MedicineDetails("Amoxicillin", "10 ml", Icon.New("pill"), Color.New("#ff8800")),
            FixedGuardianId,
            FixedInstant),
        "Medicines/MedicineDetailsUpdated.json");

    [Fact]
    public void MedicineScheduleRescheduled() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MedicineScheduleRescheduled(
            FixedMedicineId,
            new MedicineWindow(FixedTimes, FixedStartDate, FixedEndDate),
            new MedicineWindow(FixedTimes, FixedStartDate, null),
            FixedGuardianId,
            FixedInstant),
        "Medicines/MedicineScheduleRescheduled.json");

    [Fact]
    public void MedicineScheduleStopped() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MedicineScheduleStopped(FixedMedicineId, FixedGuardianId, FixedInstant),
        "Medicines/MedicineScheduleStopped.json");

    [Fact]
    public void DoseStatusChanged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new DoseStatusChanged(FixedMedicineId, FixedStartDate, new TimeOnly(8, 0), DoseStatus.Pending, DoseStatus.Taken, FixedChildId, FixedInstant),
        "Medicines/DoseStatusChanged.json");
}
