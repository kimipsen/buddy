using System.Text.Json;

using Alba;

using buddy.Features.Feelings;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Xunit;

using static buddy.IntegrationTests.Features.Feelings.FeelingsTestHelpers;

namespace buddy.IntegrationTests.Features.Feelings.ChildCheckIns;

// The child's 10-minute undo window, on a host of its own with a clock the test moves, so nothing
// sleeps (docs/backend/analysis/feelings-check-in.md, Testing).
[Collection(BuddyApiCollection.Name)]
public sealed class UndoWindowTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    // Static because the host is shared by every test in this class (BuddyApiFixture.SharedHostAsync).
    private static readonly ManualTimeProvider Clock = new();

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        // Half an hour behind the real clock: the host's other TimeProvider users (token lifetime
        // checks) then never see a Keycloak token as expired, however far a test moves the clock.
        Clock.Set(DateTimeOffset.UtcNow.AddMinutes(-30));

        _host = await fixture.SharedHostAsync(
            nameof(UndoWindowTests),
            new Dictionary<string, string?>(),
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Undo_works_at_nine_minutes()
    {
        var (childToken, checkInId) = await CheckInNowAsync();

        Clock.Advance(TimeSpan.FromMinutes(9));

        await UndoAsync(_host, childToken, checkInId);
    }

    [Fact]
    public async Task Undo_at_eleven_minutes_is_a_conflict_and_the_check_in_stays()
    {
        var (childToken, checkInId) = await CheckInNowAsync();

        Clock.Advance(TimeSpan.FromMinutes(11));

        var response = await UndoAsync(_host, childToken, checkInId, expectedStatus: 409);

        using var json = JsonDocument.Parse(await response.ReadAsTextAsync());
        Assert.Equal(UndoCheckInOutcome.UndoWindowClosedCode, json.RootElement.GetProperty("code").GetString());

        // A new check-in is always possible ("I'm green now").
        await CheckInAsync(_host, childToken, new { Zone = "Green" });
    }

    private async Task<(string ChildToken, Guid CheckInId)> CheckInNowAsync()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var checkIn = await CheckInAsync(_host, childToken, new { Zone = "Red" });

        Assert.Equal(Clock.GetUtcNow(), checkIn!.RecordedAt);

        return (childToken, checkIn.Id);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Set(DateTimeOffset now) => _now = now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
