using buddy.Features.SleepDiaries;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class SleepDiaryEventShapeTests
{
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly SleepDiaryId FixedDiaryId = SleepDiaryId.ForChild(FixedChildId);
    private static readonly SleepDiaryShareTokenId FixedTokenId = new(Guid.Parse("00000000-0000-0000-0000-0000000000a0"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedDate = new(2025, 6, 1);

    private static readonly SleepEntry FullEntry = new(
        new TimeOnly(19, 0),
        new TimeOnly(19, 30),
        new TimeOnly(20, 10),
        new TimeOnly(20, 15),
        new TimeOnly(20, 45),
        [new SleepInterval(new TimeOnly(3, 30), TimeSpan.FromMinutes(30))],
        new TimeOnly(6, 30),
        true,
        [new SleepInterval(new TimeOnly(13, 0), TimeSpan.FromMinutes(45))],
        TimeSpan.FromMinutes(555),
        "Cried before settling",
        FixedGuardianId);

    private static readonly SleepEntry PartialEntry = new(
        null, null, null, new TimeOnly(20, 0), null, [], new TimeOnly(7, 0), false, [], null, "", FixedGuardianId);

    [Fact]
    public void SleepDiaryStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SleepDiaryStarted(FixedDiaryId, FixedChildId, FixedInstant),
        "SleepDiaries/SleepDiaryStarted.json");

    [Fact]
    public void SleepEntryLogged_first_log() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SleepEntryLogged(FixedDiaryId, FixedDate, null, FullEntry, FixedInstant),
        "SleepDiaries/SleepEntryLogged.json");

    [Fact]
    public void SleepEntryLogged_overwrite() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SleepEntryLogged(FixedDiaryId, FixedDate, PartialEntry, FullEntry, FixedInstant),
        "SleepDiaries/SleepEntryLogged_Overwrite.json");

    [Fact]
    public void SleepEntryCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SleepEntryCleared(FixedDiaryId, FixedDate, PartialEntry, FixedGuardianId, FixedInstant),
        "SleepDiaries/SleepEntryCleared.json");

    [Fact]
    public void SleepHygieneNotesUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SleepHygieneNotesUpdated(FixedDiaryId, "", "No screens after 19:00", FixedGuardianId, FixedInstant),
        "SleepDiaries/SleepHygieneNotesUpdated.json");

    [Fact]
    public void SleepDiaryShareTokenCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SleepDiaryShareTokenCreated(FixedTokenId, FixedChildId, "ABC123", FixedGuardianId, FixedInstant.AddDays(30), FixedInstant),
        "SleepDiaries/SleepDiaryShareTokenCreated.json");

    [Fact]
    public void SleepDiaryShareTokenRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SleepDiaryShareTokenRevoked(FixedTokenId, FixedGuardianId, FixedInstant),
        "SleepDiaries/SleepDiaryShareTokenRevoked.json");
}
