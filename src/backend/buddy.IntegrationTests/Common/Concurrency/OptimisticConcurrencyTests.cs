using Alba;

using buddy.Common;
using buddy.Common.Concurrency;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Common.Concurrency;

// StreamVersionTracker + ConcurrencyConflictMiddleware, exercised through a real
// read-modify-append endpoint (PATCH /users/me/name). UpdateName itself is covered by
// UpdateNameTests; these only assert what optimistic concurrency adds on top.
[Collection(BuddyApiCollection.Name)]
public sealed class OptimisticConcurrencyTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_write_based_on_a_stale_stream_version_returns_conflict_and_appends_nothing()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var concurrentName = new Name("Concurrent", "Writer");

        ConcurrentWriterUserEventStore.InterleaveNextRead(userId, () =>
            new NameUpdated(new UserId(userId), new Name("Any", "Before"), concurrentName, DateTimeOffset.UtcNow));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Stale", FamilyName = "Write" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBe(409);
        });

        var envelope = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal(ConcurrencyConflictMiddleware.ErrorCode, envelope.Code);

        // The concurrent write survives; the stale one was rejected instead of overwriting it.
        var events = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        var user = User.Rehydrate(await events.ReadAsync(new UserId(userId), CancellationToken.None))!;
        Assert.Equal(concurrentName, user.Name);
    }

    [Fact]
    public async Task Retrying_after_a_conflict_succeeds_against_the_fresh_version()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();

        ConcurrentWriterUserEventStore.InterleaveNextRead(userId, () =>
            new NameUpdated(new UserId(userId), new Name("Any", "Before"), new Name("Concurrent", "Writer"), DateTimeOffset.UtcNow));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Retried", FamilyName = "Write" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBe(409);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Retried", FamilyName = "Write" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task Without_a_tracking_scope_an_append_stays_unconditional()
    {
        var (_, _, userId) = await fixture.CreateAuthenticatedUserAsync();
        var events = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        var id = new UserId(userId);

        Assert.False(StreamVersionTracker.IsTracking);

        var before = User.Rehydrate(await events.ReadAsync(id, CancellationToken.None))!;
        await events.AppendAsync(id, [new NameUpdated(id, before.Name, new Name("First", "Write"), DateTimeOffset.UtcNow)], CancellationToken.None);
        await events.AppendAsync(id, [new NameUpdated(id, before.Name, new Name("Second", "Write"), DateTimeOffset.UtcNow)], CancellationToken.None);

        Assert.Equal(new Name("Second", "Write"), User.Rehydrate(await events.ReadAsync(id, CancellationToken.None))!.Name);
    }

    [Fact]
    public async Task Within_a_tracking_scope_an_append_after_an_outside_write_throws_a_concurrency_exception()
    {
        var (_, _, userId) = await fixture.CreateAuthenticatedUserAsync();
        var events = fixture.Host.Services.GetRequiredService<MartenUserEventStore>();
        var marten = fixture.Host.Services.GetRequiredService<IUsersStore>();
        var id = new UserId(userId);

        using (StreamVersionTracker.BeginScope())
        {
            var before = User.Rehydrate(await events.ReadAsync(id, CancellationToken.None))!;

            await using (var session = marten.LightweightSession())
            {
                session.Events.Append(userId, new NameUpdated(id, before.Name, new Name("Outside", "Write"), DateTimeOffset.UtcNow));
                await session.SaveChangesAsync();
            }

            await Assert.ThrowsAnyAsync<JasperFx.ConcurrencyException>(() =>
                events.AppendAsync(id, [new NameUpdated(id, before.Name, new Name("Stale", "Write"), DateTimeOffset.UtcNow)], CancellationToken.None));
        }
    }
}
