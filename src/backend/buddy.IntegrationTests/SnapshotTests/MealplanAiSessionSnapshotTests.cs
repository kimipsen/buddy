using Alba;

using buddy.Features.Mealplans;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans.AiAssistant;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (MealplanAiSessionSnapshotProjection, schema "snapshots")
// stays exactly consistent with a full replay-from-events rehydration after a sequence of
// commands -- the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say
// events remain the single source of truth and the snapshot is just a derived, quicker-to-read
// cache of the same state.
[Collection(BuddyApiCollection.Name)]
public sealed class MealplanAiSessionSnapshotTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly From = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly To = From.AddDays(2);

    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);

        // AiSessionStarted.
        var view = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, From, To, [MealSlot.Dinner, MealSlot.Lunch]);

        var sessions = fixture.Host.Services.GetRequiredService<IAiSessionEventStore>();
        var latest = await sessions.FindLatestForChildAsync(new UserId(child.Id), CancellationToken.None);
        Assert.NotNull(latest);
        var id = latest.Value.Id;
        Assert.Equal(view.Id, id.Value);

        // AiSessionDiscarded -- doesn't require a real provider call, just like StartAiSession.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{child.Id}/ai/sessions/current/discard");
            _.StatusCodeShouldBeOk();
        });

        var events = await sessions.ReadAsync(id, CancellationToken.None);
        var replayed = MealplanAiSession.Rehydrate(events);
        var snapshot = await sessions.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
