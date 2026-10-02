using buddy.Features.Calendars;
using buddy.Features.Progress;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class ProgressEventShapeTests
{
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly ProgressId FixedProgressId = ProgressId.ForChild(FixedChildId);
    private static readonly CalendarItemId FixedItemId = new(Guid.Parse("00000000-0000-0000-0000-000000000030"));
    private static readonly Guid FixedSubtaskId = Guid.Parse("00000000-0000-0000-0000-000000000031");
    private static readonly DateOnly FixedDate = new(2025, 6, 1);
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProgressStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ProgressStarted(FixedProgressId, FixedChildId, FixedInstant),
        "Progress/ProgressStarted.json");

    [Fact]
    public void StarAwarded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new StarAwarded(FixedProgressId, FixedItemId, FixedDate, FixedInstant, new CompletionTarget.WholeTask()),
        "Progress/StarAwarded.json");

    [Fact]
    public void StarAwarded_ForSubtask() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new StarAwarded(FixedProgressId, FixedItemId, FixedDate, FixedInstant, new CompletionTarget.Subtask(FixedSubtaskId)),
        "Progress/StarAwarded_ForSubtask.json");

    [Fact]
    public void StarAwarded_ReadsBack() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new StarAwarded(FixedProgressId, FixedItemId, FixedDate, FixedInstant, new CompletionTarget.WholeTask()),
        "Progress/StarAwarded.json");

    [Fact]
    public void StarAwarded_ForSubtask_ReadsBack() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new StarAwarded(FixedProgressId, FixedItemId, FixedDate, FixedInstant, new CompletionTarget.Subtask(FixedSubtaskId)),
        "Progress/StarAwarded_ForSubtask.json");

    [Fact]
    public void StarRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new StarRevoked(FixedProgressId, FixedItemId, FixedDate, FixedInstant, new CompletionTarget.WholeTask()),
        "Progress/StarRevoked.json");

    [Fact]
    public void StarRevoked_ForSubtask() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new StarRevoked(FixedProgressId, FixedItemId, FixedDate, FixedInstant, new CompletionTarget.Subtask(FixedSubtaskId)),
        "Progress/StarRevoked_ForSubtask.json");

    [Fact]
    public void MilestoneUnlocked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new MilestoneUnlocked(FixedProgressId, 10, FixedInstant),
        "Progress/MilestoneUnlocked.json");

    [Fact]
    public void GoalPostsConfigured() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GoalPostsConfigured(FixedProgressId, [new GoalPost(5, "star", "Movie night"), new GoalPost(20, "trophy", "")], FixedInstant),
        "Progress/GoalPostsConfigured.json");
}
