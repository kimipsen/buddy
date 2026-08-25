using buddy.Features.Pickups;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class PickupEventShapeTests
{
    private static readonly PickupScheduleId FixedScheduleId = new(Guid.Parse("00000000-0000-0000-0000-000000000070"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly UserId FixedSiblingId = new(Guid.Parse("00000000-0000-0000-0000-000000000004"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedDate = new(2025, 6, 1);

    [Fact]
    public void PickupScheduleCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PickupScheduleCreated(FixedScheduleId, FixedChildId, FixedInstant),
        "Pickups/PickupScheduleCreated.json");

    [Fact]
    public void PickupAssigned_with_a_guardian_assignee() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PickupAssigned(
            FixedScheduleId, FixedDate, PickupSlot.DropOff, Before: null,
            new PickupAssignment(PickupAssigneeKind.Guardian, FixedGuardianId, null, null, null, null, new TimeOnly(8, 0), FixedGuardianId, "Bring an umbrella"),
            FixedInstant),
        "Pickups/PickupAssigned_Guardian.json");

    [Fact]
    public void PickupAssigned_with_a_self_escort_assignee() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PickupAssigned(
            FixedScheduleId, FixedDate, PickupSlot.PickUp, Before: null,
            new PickupAssignment(PickupAssigneeKind.SelfEscort, null, null, null, null, null, null, FixedGuardianId, null),
            FixedInstant),
        "Pickups/PickupAssigned_SelfEscort.json");

    [Fact]
    public void PickupAssigned_with_a_sibling_assignee() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PickupAssigned(
            FixedScheduleId, FixedDate, PickupSlot.PickUp, Before: null,
            new PickupAssignment(PickupAssigneeKind.Sibling, null, FixedSiblingId, null, null, null, null, FixedGuardianId, null),
            FixedInstant),
        "Pickups/PickupAssigned_Sibling.json");

    [Fact]
    public void PickupAssigned_with_a_playdate_assignee() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PickupAssigned(
            FixedScheduleId, FixedDate, PickupSlot.PickUp, Before: null,
            new PickupAssignment(PickupAssigneeKind.Playdate, null, null, "Mia's mom", "Mia's house", "+45 12 34 56 78", null, FixedGuardianId, null),
            FixedInstant),
        "Pickups/PickupAssigned_Playdate.json");

    [Fact]
    public void PickupCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PickupCleared(
            FixedScheduleId, FixedDate, PickupSlot.PickUp,
            new PickupAssignment(PickupAssigneeKind.SelfEscort, null, null, null, null, null, null, FixedGuardianId, null),
            FixedGuardianId,
            FixedInstant),
        "Pickups/PickupCleared.json");
}
