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
    private static readonly ChecklistSuggestionId FixedSuggestionId = new(Guid.Parse("00000000-0000-0000-0000-000000000073"));

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

    // Packing checklists (docs/backend/analysis/packing-checklists.md). Kind and Weekdays are left
    // out of the JSON at their defaults, so the golden files above are unchanged; these pin the
    // checklist shapes, and the read-back tests prove pre-checklist events replay as routines.
    [Fact]
    public void TaskTemplateCreated_Checklist() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TaskTemplateCreated(FixedTemplateId, FixedChildId, FixedGuardianId, "School bag", Icon.New("backpack"), Color.New("#3355ff"), FixedInstant, TaskTemplateKind.Checklist),
        "TaskLibrary/TaskTemplateCreated_Checklist.json");

    [Fact]
    public void A_task_template_created_before_checklists_reads_back_as_a_routine() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new TaskTemplateCreated(FixedTemplateId, FixedChildId, FixedGuardianId, "Morning routine", Icon.New("sunrise"), Color.New("#ffaa00"), FixedInstant, TaskTemplateKind.Routine),
        "TaskLibrary/TaskTemplateCreated.json");

    [Fact]
    public void SubtaskAdded_WithWeekdays() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new SubtaskAdded(
            FixedTemplateId,
            new Subtask(FixedSubtaskId, "Gym clothes", null, TimeSpan.Zero, Weekdays.Tuesday | Weekdays.Thursday),
            0,
            FixedGuardianId,
            FixedInstant),
        "TaskLibrary/SubtaskAdded_WithWeekdays.json");

    [Fact]
    public void A_subtask_added_before_weekdays_reads_back_with_no_weekday_filter() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new SubtaskAdded(
            FixedTemplateId,
            new Subtask(FixedSubtaskId, "Brush teeth", Icon.New("toothbrush"), TimeSpan.FromMinutes(2), Weekdays.None),
            0,
            FixedGuardianId,
            FixedInstant),
        "TaskLibrary/SubtaskAdded.json");

    [Fact]
    public void A_subtask_with_weekdays_reads_back_with_them() => EventShapeTestSupport.AssertGoldenFileReadsBackAs(
        new SubtaskAdded(
            FixedTemplateId,
            new Subtask(FixedSubtaskId, "Gym clothes", null, TimeSpan.Zero, Weekdays.Tuesday | Weekdays.Thursday),
            0,
            FixedGuardianId,
            FixedInstant),
        "TaskLibrary/SubtaskAdded_WithWeekdays.json");

    [Fact]
    public void ChecklistOptionsChanged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ChecklistOptionsChanged(FixedTemplateId, ChecklistOptions.None, new ChecklistOptions(new TimeOnly(17, 0)), FixedGuardianId, FixedInstant),
        "TaskLibrary/ChecklistOptionsChanged.json");

    [Fact]
    public void ChecklistItemSuggested() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ChecklistItemSuggested(FixedTemplateId, FixedSuggestionId, "Recorder", Weekdays.Friday, FixedChildId, FixedInstant),
        "TaskLibrary/ChecklistItemSuggested.json");

    [Fact]
    public void ChecklistItemSuggestionApproved() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ChecklistItemSuggestionApproved(FixedTemplateId, FixedSuggestionId, FixedSubtaskId, FixedGuardianId, FixedInstant),
        "TaskLibrary/ChecklistItemSuggestionApproved.json");

    [Fact]
    public void ChecklistItemSuggestionDeclined() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ChecklistItemSuggestionDeclined(FixedTemplateId, FixedSuggestionId, FixedGuardianId, FixedInstant),
        "TaskLibrary/ChecklistItemSuggestionDeclined.json");

    [Fact]
    public void ChecklistItemSuggestionWithdrawn() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ChecklistItemSuggestionWithdrawn(FixedTemplateId, FixedSuggestionId, FixedChildId, FixedInstant),
        "TaskLibrary/ChecklistItemSuggestionWithdrawn.json");
}
