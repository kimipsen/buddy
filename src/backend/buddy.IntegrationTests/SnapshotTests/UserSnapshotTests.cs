using Alba;

using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

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

    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_a_guardian_turns_visual_timers_off()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { ShowVisualTimers = false }).ToUrl($"/users/me/children/{child.Id}/visual-timers");
            _.StatusCodeShouldBeOk();
        });

        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        var id = new UserId(child.Id);

        var replayed = User.Rehydrate(await users.ReadAsync(id, CancellationToken.None));
        var snapshot = await users.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.False(snapshot.ShowVisualTimers);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }

    [Fact]
    public async Task The_snapshot_matches_a_full_replay_after_a_guardian_changes_homework_lengths()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { FocusMinutes = 10, BreakMinutes = 2 }).ToUrl($"/users/me/children/{child.Id}/homework-chunks");
            _.StatusCodeShouldBeOk();
        });

        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        var id = new UserId(child.Id);

        var replayed = User.Rehydrate(await users.ReadAsync(id, CancellationToken.None));
        var snapshot = await users.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(new HomeworkChunks(10, 2), snapshot.HomeworkChunks);
        Assert.Equivalent(replayed, snapshot, strict: true);
    }

    // A snapshot row written before homework chunks existed has no such property; the constructor
    // gets null and the property reads it as the default lengths, without a rebuild.
    [Fact]
    public async Task A_snapshot_row_without_homework_chunks_reads_as_the_default()
    {
        var (_, _, userId) = await fixture.CreateAuthenticatedUserAsync();
        var dataSource = fixture.Host.Services.GetRequiredService<NpgsqlDataSource>();

        await using (var command = dataSource.CreateCommand(
            "UPDATE snapshots.mt_doc_usersnapshot SET data = data #- '{User,HomeworkChunks}' #- '{User,homeworkChunks}' WHERE id = $1"))
        {
            command.Parameters.AddWithValue(userId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        await using (var check = dataSource.CreateCommand(
            "SELECT data::text FROM snapshots.mt_doc_usersnapshot WHERE id = $1"))
        {
            check.Parameters.AddWithValue(userId);
            var json = (string?)await check.ExecuteScalarAsync();
            Assert.DoesNotContain("HomeworkChunks", json!, StringComparison.OrdinalIgnoreCase);
        }

        var snapshot = await fixture.Host.Services.GetRequiredService<IUserEventStore>()
            .FindSnapshotAsync(new UserId(userId), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(HomeworkChunks.Default, snapshot.HomeworkChunks);
    }

    // A snapshot row written before ShowVisualTimers existed has no such property; System.Text.Json
    // falls back to the constructor parameter's default, so it reads as true without a rebuild.
    [Fact]
    public async Task A_snapshot_row_without_the_visual_timer_property_reads_as_shown()
    {
        var (_, _, userId) = await fixture.CreateAuthenticatedUserAsync();
        var dataSource = fixture.Host.Services.GetRequiredService<NpgsqlDataSource>();

        await using (var command = dataSource.CreateCommand(
            "UPDATE snapshots.mt_doc_usersnapshot SET data = data #- '{User,ShowVisualTimers}' #- '{User,showVisualTimers}' WHERE id = $1"))
        {
            command.Parameters.AddWithValue(userId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        await using (var check = dataSource.CreateCommand(
            "SELECT data::text FROM snapshots.mt_doc_usersnapshot WHERE id = $1"))
        {
            check.Parameters.AddWithValue(userId);
            var json = (string?)await check.ExecuteScalarAsync();
            Assert.DoesNotContain("ShowVisualTimers", json!, StringComparison.OrdinalIgnoreCase);
        }

        var snapshot = await fixture.Host.Services.GetRequiredService<IUserEventStore>()
            .FindSnapshotAsync(new UserId(userId), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.ShowVisualTimers);
    }
}
