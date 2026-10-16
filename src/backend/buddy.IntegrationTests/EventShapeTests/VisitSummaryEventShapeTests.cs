using buddy.Features.Users;
using buddy.Features.VisitSummaries;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class VisitSummaryEventShapeTests
{
    private static readonly VisitSummaryId FixedSummaryId = new(Guid.Parse("00000000-0000-0000-0000-0000000000c0"));
    private static readonly VisitSummaryShareLinkId FixedLinkId = new(Guid.Parse("00000000-0000-0000-0000-0000000000c1"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly VisitSummaryDetails Details = new(
        "Follow-up with Dr. Lund",
        new DateOnly(2025, 6, 30),
        new DateOnly(2025, 6, 1),
        new DateOnly(2025, 6, 28),
        [VisitSummarySection.Medicine, VisitSummarySection.Sleep, VisitSummarySection.Feelings],
        "Afternoons are harder since the dose change.");

    private static readonly VisitSummaryDetails Edited = Details with
    {
        VisitDate = null,
        Sections = [VisitSummarySection.Medicine],
        Note = ""
    };

    [Fact]
    public void VisitSummaryCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new VisitSummaryCreated(FixedSummaryId, FixedChildId, Details, FixedGuardianId, FixedInstant),
        "VisitSummaries/VisitSummaryCreated.json");

    [Fact]
    public void VisitSummaryUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new VisitSummaryUpdated(FixedSummaryId, Details, Edited, FixedGuardianId, FixedInstant),
        "VisitSummaries/VisitSummaryUpdated.json");

    [Fact]
    public void VisitSummaryDeleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new VisitSummaryDeleted(FixedSummaryId, FixedGuardianId, FixedInstant),
        "VisitSummaries/VisitSummaryDeleted.json");

    [Fact]
    public void VisitSummaryShareLinkCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new VisitSummaryShareLinkCreated(FixedSummaryId, FixedLinkId, "ABCDEF0123456789", FixedInstant.AddDays(30), FixedGuardianId, FixedInstant),
        "VisitSummaries/VisitSummaryShareLinkCreated.json");

    [Fact]
    public void VisitSummaryShareLinkCreated_without_expiry() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new VisitSummaryShareLinkCreated(FixedSummaryId, FixedLinkId, "ABCDEF0123456789", null, FixedGuardianId, FixedInstant),
        "VisitSummaries/VisitSummaryShareLinkCreated_NoExpiry.json");

    [Fact]
    public void VisitSummaryShareLinkRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new VisitSummaryShareLinkRevoked(FixedSummaryId, FixedLinkId, FixedGuardianId, FixedInstant),
        "VisitSummaries/VisitSummaryShareLinkRevoked.json");
}
