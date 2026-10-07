using Alba;

using buddy.Common.Erasure;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// GDPR Question 6.2 (docs/backend/analysis/gdpr-data-protection.md). The sweep is run directly with
// a "now" in the future instead of waiting for AiSessionRetentionService's daily tick. It erases
// every due session in the shared database, which other tests never read back.
[Collection(BuddyApiCollection.Name)]
public sealed class AiSessionRetentionTests(BuddyApiFixture fixture)
{
    private const string Notes = "Alex won't eat fish since the hospital stay";

    [Fact]
    public async Task A_session_closed_more_than_30_days_ago_has_its_conversation_erased()
    {
        var (guardianToken, childId, sessionId) = await StartAndDiscardSessionAsync();

        await EraseExpiredAsync(DateTimeOffset.UtcNow.AddDays(31));

        var events = await ReadSessionAsync(sessionId);
        var started = Assert.Single(events.Select(e => e switch { AiSessionStarted s => s, _ => null }).OfType<AiSessionStarted>());
        Assert.Equal(Erased.Text, started.Notes);

        // The session itself, its status and its dates stay.
        var session = await FindIndexAsync(sessionId);
        Assert.NotNull(session.ContentErasedAt);
        Assert.Equal(AiSessionStatus.Discarded, MealplanAiSession.Replay(events).Status);

        var view = await GetCurrentSessionAsync(guardianToken, childId);
        Assert.Equal(AiSessionStatus.Discarded, view.Status);
    }

    [Fact]
    public async Task An_idle_session_nobody_closed_is_closed_before_its_conversation_is_erased()
    {
        var (guardianToken, childId, sessionId) = await StartSessionAsync();

        await EraseExpiredAsync(DateTimeOffset.UtcNow.AddDays(31));

        var events = await ReadSessionAsync(sessionId);
        Assert.Equal(nameof(AiSessionExpired), events.Last().EventType);
        Assert.Equal(AiSessionStatus.Discarded, MealplanAiSession.Replay(events).Status);

        var snapshot = await fixture.Host.Services.GetRequiredService<IAiSessionEventStore>()
            .FindSnapshotAsync(new MealplanAiSessionId(sessionId), CancellationToken.None);
        Assert.Equal(AiSessionStatus.Discarded, snapshot!.Status);

        // The expiry is the session's last activity now, and a closed session takes no messages.
        var index = await FindIndexAsync(sessionId);
        Assert.Equal(index.ContentErasedAt, index.LastActivityAt);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Text = "Carry on" }).ToUrl($"/mealplans/children/{childId}/ai/sessions/current/messages");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_session_closed_less_than_30_days_ago_is_kept()
    {
        var (_, _, sessionId) = await StartAndDiscardSessionAsync();

        await EraseExpiredAsync(DateTimeOffset.UtcNow.AddDays(29));

        var started = (await ReadSessionAsync(sessionId)).Select(e => e switch { AiSessionStarted s => s, _ => null }).OfType<AiSessionStarted>().Single();
        Assert.Equal(Notes, started.Notes);
        Assert.Null((await FindIndexAsync(sessionId)).ContentErasedAt);
    }

    [Fact]
    public async Task A_session_from_before_retention_is_judged_by_its_events()
    {
        // A row written before LastActivityAt existed is a candidate by its StartedAt, but the
        // stream shows recent activity, so it is kept and the row gets its LastActivityAt.
        var (_, _, sessionId) = await StartAndDiscardSessionAsync();
        var index = await FindIndexAsync(sessionId);
        await UpdateIndexAsync(index with { StartedAt = index.StartedAt.AddDays(-40), LastActivityAt = null });

        await EraseExpiredAsync(DateTimeOffset.UtcNow);

        var updated = await FindIndexAsync(sessionId);
        Assert.Null(updated.ContentErasedAt);
        Assert.NotNull(updated.LastActivityAt);
        Assert.True(updated.LastActivityAt > updated.StartedAt.AddDays(30));
    }

    [Fact]
    public async Task Every_append_moves_the_last_activity_forward()
    {
        var (_, _, sessionId) = await StartAndDiscardSessionAsync();
        var index = await FindIndexAsync(sessionId);

        // Starting sets it; discarding (an append) moves it to the discard.
        Assert.NotNull(index.LastActivityAt);
        Assert.True(index.LastActivityAt > index.StartedAt);
    }

    private async Task<(string GuardianToken, Guid ChildId, Guid SessionId)> StartSessionAsync()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await AiAssistantTestHelpers.ConfigureAnthropicKeyAsync(fixture, guardianToken, child.Id);
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var session = await AiAssistantTestHelpers.StartSessionAsync(fixture, guardianToken, child.Id, from, from.AddDays(2), [MealSlot.Dinner], notes: Notes);

        return (guardianToken, child.Id, session.Id);
    }

    private async Task<(string GuardianToken, Guid ChildId, Guid SessionId)> StartAndDiscardSessionAsync()
    {
        var (guardianToken, childId, sessionId) = await StartSessionAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/mealplans/children/{childId}/ai/sessions/current/discard");
            _.StatusCodeShouldBeOk();
        });

        return (guardianToken, childId, sessionId);
    }

    private async Task<AiSessionViewDto> GetCurrentSessionAsync(string guardianToken, Guid childId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/mealplans/children/{childId}/ai/sessions/current");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<AiSessionViewDto>();
    }

    private async Task EraseExpiredAsync(DateTimeOffset now)
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AiSessionRetention>().EraseExpiredAsync(now, CancellationToken.None);
    }

    private async Task<IReadOnlyCollection<MealplanAiSessionEvent>> ReadSessionAsync(Guid sessionId) =>
        await fixture.Host.Services.GetRequiredService<IAiSessionEventStore>().ReadAsync(new MealplanAiSessionId(sessionId), CancellationToken.None);

    private async Task<AiSessionIndexDocument> FindIndexAsync(Guid sessionId)
    {
        await using var session = fixture.Host.Services.GetRequiredService<IMealplansStore>().QuerySession();
        return (await session.LoadAsync<AiSessionIndexDocument>(sessionId, CancellationToken.None))!;
    }

    private async Task UpdateIndexAsync(AiSessionIndexDocument index) =>
        await fixture.Host.Services.GetRequiredService<IAiSessionEventStore>().UpdateIndexAsync(index, CancellationToken.None);
}
