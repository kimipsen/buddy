using Alba;

using buddy.Features.TaskLibrary;
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
}
