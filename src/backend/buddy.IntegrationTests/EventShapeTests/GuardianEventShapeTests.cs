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
}
