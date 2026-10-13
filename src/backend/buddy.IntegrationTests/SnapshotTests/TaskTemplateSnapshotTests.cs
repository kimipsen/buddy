using System.Text;

using Alba;

using buddy.Features.TaskLibrary;
using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (TaskTemplateSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain
// the single source of truth and the snapshot is just a derived, quicker-to-read cache of the
// same state.
[Collection(BuddyApiCollection.Name)]
public sealed class TaskTemplateSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, guardianToken, child.Id);
        Assert.NotNull(template);

        var withSubtask = await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, template.Id, new AddSubtaskOptions(Title: "Brush teeth", Duration: "00:02:00"));
        Assert.NotNull(withSubtask);
        var subtaskId = Assert.Single(withSubtask.Subtasks).Id;

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { Title = "Brush teeth thoroughly", Icon = "toothbrush", Duration = "00:03:00" })
                .ToUrl($"/task-templates/{template.Id}/subtasks/{subtaskId}");
            _.StatusCodeShouldBeOk();
        });

        var templates = fixture.Host.Services.GetRequiredService<ITaskTemplateEventStore>();
        var id = new TaskTemplateId(template.Id);

        var events = await templates.ReadAsync(id, CancellationToken.None);
        var replayed = TaskTemplate.Rehydrate(events);
        var snapshot = await templates.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }

    // A packing list's options and its child suggestions (suggested, approved, declined, withdrawn)
    // fold into the snapshot the same way (docs/backend/analysis/packing-checklists.md).
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_for_a_packing_list_with_suggestions()
    {
        var family = await ChecklistTestHelpers.CreateFamilyAsync(fixture);
        var checklist = await ChecklistTestHelpers.CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id, eveningBeforeFrom: "17:00:00");
        await ChecklistTestHelpers.AddItemAsync(fixture, family.GuardianToken, checklist.Id, "Gym clothes", ["Tuesday"]);
        var approved = await ChecklistTestHelpers.SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Recorder", ["Friday"]);
        await ChecklistTestHelpers.ApproveAsync(fixture, family.GuardianToken, checklist.Id, approved, "Recorder", ["Friday"]);
        var declined = await ChecklistTestHelpers.SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Sweets");
        await ChecklistTestHelpers.ResolveAsync(fixture, family.GuardianToken, checklist.Id, declined, "decline");
        var withdrawn = await ChecklistTestHelpers.SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Sun hat");
        await ChecklistTestHelpers.ResolveAsync(fixture, family.ChildToken, checklist.Id, withdrawn, "withdraw");
        await ChecklistTestHelpers.SuggestIdAsync(fixture, family.ChildToken, checklist.Id, "Water bottle");

        var templates = fixture.Host.Services.GetRequiredService<ITaskTemplateEventStore>();
        var id = new TaskTemplateId(checklist.Id);

        var replayed = TaskTemplate.Rehydrate(await templates.ReadAsync(id, CancellationToken.None));
        var snapshot = await templates.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.Equal(TaskTemplateKind.Checklist, snapshot.Kind);
        Assert.Equal(new TimeOnly(17, 0), snapshot.Checklist.EveningBeforeFrom);
        Assert.Equal(Weekdays.Tuesday, snapshot.Subtasks[0].Weekdays);
        Assert.Equal(4, snapshot.Suggestions.Count);
    }

    // A snapshot stored before packing checklists has no Kind, Checklist or Suggestions, and its
    // subtasks no Weekdays. It must read as a routine with no options and no suggestions.
    [Fact]
    public void A_snapshot_from_before_checklists_still_loads_as_a_routine()
    {
        const string before = """
            {
              "Id": "00000000-0000-0000-0000-000000000070",
              "TaskTemplate": {
                "Id": "00000000-0000-0000-0000-000000000070",
                "CreatedBy": "00000000-0000-0000-0000-000000000001",
                "Name": "Morning routine",
                "Icon": "sunrise",
                "Color": "#ffaa00",
                "Subtasks": [
                  { "Id": "00000000-0000-0000-0000-000000000071", "Title": "Brush teeth", "Icon": null, "Duration": "00:02:00" }
                ],
                "IsArchived": false,
                "LastModifiedBy": "00000000-0000-0000-0000-000000000001",
                "TotalDuration": "00:02:00"
              }
            }
            """;

        var serializer = fixture.Host.Services.GetRequiredService<ITaskLibraryStore>().Options.Serializer();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(before));
        var template = serializer.FromJson<TaskTemplateSnapshot>(stream).TaskTemplate;

        Assert.Equal(TaskTemplateKind.Routine, template.Kind);
        Assert.Equal(ChecklistOptions.None, template.Checklist);
        Assert.Empty(template.Suggestions);
        Assert.Equal(Weekdays.None, Assert.Single(template.Subtasks).Weekdays);
        Assert.True(template.Subtasks[0].AppliesOn(new DateOnly(2026, 6, 2)));
    }
}
