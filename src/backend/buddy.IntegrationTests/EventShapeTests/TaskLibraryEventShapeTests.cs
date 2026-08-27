using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class TaskLibraryEventShapeTests
{
    private static readonly TaskTemplateId FixedTemplateId = new(Guid.Parse("00000000-0000-0000-0000-000000000070"));
    private static readonly SubtaskId FixedSubtaskId = new(Guid.Parse("00000000-0000-0000-0000-000000000071"));
    private static readonly SubtaskId FixedSubtaskId2 = new(Guid.Parse("00000000-0000-0000-0000-000000000072"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TaskTemplateCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TaskTemplateCreated(FixedTemplateId, FixedChildId, FixedGuardianId, "Morning routine", Icon.New("sunrise"), Color.New("#ffaa00"), FixedInstant),
        "TaskLibrary/TaskTemplateCreated.json");

    [Fact]
    public void TaskTemplateDetailsUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TaskTemplateDetailsUpdated(
            FixedTemplateId,
            new TaskTemplateDetails("Morning routine", Icon.New("sunrise"), Color.New("#ffaa00")),
            new TaskTemplateDetails("Evening routine", Icon.New("moon"), Color.New("#3355ff")),
            FixedGuardianId,
            FixedInstant),
        "TaskLibrary/TaskTemplateDetailsUpdated.json");

    [Fact]
    public void SubtaskAdded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SubtaskAdded(
            FixedTemplateId,
            new Subtask(FixedSubtaskId, "Brush teeth", Icon.New("toothbrush"), TimeSpan.FromMinutes(2)),
            0,
            FixedGuardianId,
            FixedInstant),
        "TaskLibrary/SubtaskAdded.json");

    [Fact]
    public void SubtaskUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SubtaskUpdated(
            FixedTemplateId,
            FixedSubtaskId,
            new Subtask(FixedSubtaskId, "Brush teeth", Icon.New("toothbrush"), TimeSpan.FromMinutes(2)),
            new Subtask(FixedSubtaskId, "Brush teeth thoroughly", Icon.New("toothbrush"), TimeSpan.FromMinutes(3)),
            FixedGuardianId,
            FixedInstant),
        "TaskLibrary/SubtaskUpdated.json");

    [Fact]
    public void SubtaskRemoved() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SubtaskRemoved(FixedTemplateId, FixedSubtaskId, FixedGuardianId, FixedInstant),
        "TaskLibrary/SubtaskRemoved.json");

    [Fact]
    public void SubtasksReordered() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SubtasksReordered(FixedTemplateId, [FixedSubtaskId2, FixedSubtaskId], FixedGuardianId, FixedInstant),
        "TaskLibrary/SubtasksReordered.json");

    [Fact]
    public void TaskTemplateArchived() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TaskTemplateArchived(FixedTemplateId, FixedGuardianId, FixedInstant),
        "TaskLibrary/TaskTemplateArchived.json");
}
