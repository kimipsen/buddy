using Alba;

using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline Marten snapshot (UserSnapshotProjection, schema "snapshots") stays exactly
// consistent with a full replay-from-events rehydration after a sequence of commands -- the
// invariant docs/backend/analysis/event-stream-snapshots.md relies on to say events remain the
// single source of truth and the snapshot is just a derived, quicker-to-read cache of the same
// state.
[Collection(BuddyApiCollection.Name)]
public sealed class UserSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_several_commands()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Updated", FamilyName = "Person" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBeOk();
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { TimeZoneId = "Europe/Copenhagen" }).ToUrl("/users/me/timezone");
            _.StatusCodeShouldBeOk();
        });

        // Leaves a pending verification, so EmailVerificationJsonConverter round-trips Pending.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = $"changed-{Guid.NewGuid():N}@buddy.test" }).ToUrl("/users/me/email");
            _.StatusCodeShouldBeOk();
        });

        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        var id = new UserId(userId);

        var events = await users.ReadAsync(id, CancellationToken.None);
        var replayed = User.Rehydrate(events);
        var snapshot = await users.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equivalent(replayed, snapshot, strict: true);
        Assert.True(snapshot.EmailVerification is EmailVerification.Pending);
        Assert.Equal(replayed.EmailVerification, snapshot.EmailVerification);
    }
}
