using buddy.Features.Mealplans;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Alba;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (AiProviderCredentialSnapshotProjection, schema
// "snapshots") stays exactly consistent with a full replay-from-events rehydration after a
// sequence of commands -- the invariant docs/backend/analysis/event-stream-snapshots.md relies
// on to say events remain the single source of truth and the snapshot is just a derived,
// quicker-to-read cache of the same state.
[Collection(BuddyApiCollection.Name)]
public sealed class AiProviderCredentialSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        // AiCredentialsInitialized + ProviderApiKeySet.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-ant-test1234" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        // A second ProviderApiKeySet.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Json(new { ApiKey = "sk-oai-test5678" }).ToUrl($"/mealplans/children/{child.Id}/ai/providers/OpenAi/key");
            _.StatusCodeShouldBeOk();
        });

        // ActiveProviderChanged.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/mealplans/children/{child.Id}/ai/active-provider/OpenAi");
            _.StatusCodeShouldBeOk();
        });

        // ProviderApiKeyRemoved.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/mealplans/children/{child.Id}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

        var credentials = fixture.Host.Services.GetRequiredService<IAiCredentialEventStore>();
        var id = await credentials.FindIdForChildAsync(new UserId(child.Id), CancellationToken.None);
        Assert.NotNull(id);

        var events = await credentials.ReadAsync(id, CancellationToken.None);
        var replayed = AiProviderCredential.Rehydrate(events);
        var snapshot = await credentials.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
