using buddy.Features.Guardians;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class GuardianEventShapeTests
{
    private static readonly GuardianLinkId FixedLinkId = new(Guid.Parse("00000000-0000-0000-0000-000000000020"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GuardianLinked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GuardianLinked(FixedLinkId, FixedChildId, FixedGuardianId, GuardianKind.Guardian, FixedInstant),
        "Guardians/GuardianLinked.json");

    [Fact]
    public void GuardianKindChanged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GuardianKindChanged(FixedLinkId, GuardianKind.Guardian, GuardianKind.Parent, FixedInstant),
        "Guardians/GuardianKindChanged.json");

    [Fact]
    public void GuardianRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GuardianRevoked(FixedLinkId, FixedInstant),
        "Guardians/GuardianRevoked.json");

    private static readonly GuardianInviteId FixedInviteId = new(Guid.Parse("00000000-0000-0000-0000-000000000021"));
    private static readonly UserId FixedInviteeId = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly DateTimeOffset FixedExpiry = new(2025, 1, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GuardianInviteCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GuardianInviteCreated(FixedInviteId, FixedChildId, "Alex", "invitee@buddy.test", GuardianKind.Parent, FixedGuardianId, "deadbeef", FixedExpiry, FixedInstant),
        "Guardians/GuardianInviteCreated.json");

    [Fact]
    public void GuardianInviteAccepted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GuardianInviteAccepted(FixedInviteId, FixedInviteeId, FixedInstant),
        "Guardians/GuardianInviteAccepted.json");

    [Fact]
    public void GuardianInviteRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GuardianInviteRevoked(FixedInviteId, FixedGuardianId, FixedInstant),
        "Guardians/GuardianInviteRevoked.json");
}
