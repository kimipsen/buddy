using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (CalendarSnapshotProjection, schema "snapshots") stays
// exactly consistent with a full replay-from-events rehydration after a sequence of commands --
// the invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain
// the single source of truth and the snapshot is just a derived, quicker-to-read cache of the
// same state.
[Collection(BuddyApiCollection.Name)]
public sealed class CalendarSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Snapshot Check");

        var (_, _, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Contributor }).ToUrl($"/calendars/{calendarId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/calendars/{calendarId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var calendars = fixture.Host.Services.GetRequiredService<ICalendarEventStore>();
        var id = new CalendarId(calendarId);

        var events = await calendars.ReadAsync(id, CancellationToken.None);
        var replayed = Calendar.Rehydrate(events);
        var snapshot = await calendars.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }
}
