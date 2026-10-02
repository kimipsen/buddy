using buddy.Common.Aggregates;
using buddy.Features.Guardians;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.Common.Aggregates;

// Start/Advance replay (docs/backend/analysis/eliminate-nulls.md, Phase 2), exercised through a
// real aggregate so the wiring each aggregate uses is covered too. Pure in-memory, no fixture.
public sealed class EventReplayTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly GuardianLinkId LinkId = new(Guid.Parse("0191e3a0-0000-7000-8000-0000000000a1"));
    private static readonly UserId ChildId = new(Guid.Parse("0191e3a0-0000-7000-8000-0000000000a2"));
    private static readonly UserId GuardianId = new(Guid.Parse("0191e3a0-0000-7000-8000-0000000000a3"));

    private static GuardianEvent Linked => GuardianEvent.FromPayload(new GuardianLinked(LinkId, ChildId, GuardianId, GuardianKind.Parent, At));

    private static GuardianEvent Revoked => GuardianEvent.FromPayload(new GuardianRevoked(LinkId, At));

    [Fact]
    public void An_empty_stream_rehydrates_to_null()
    {
        Assert.Null(GuardianLink.Rehydrate([]));
    }

    [Fact]
    public void Replaying_an_empty_stream_throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GuardianLink.Replay([]));
        Assert.Contains(nameof(GuardianLink), error.Message);
    }

    [Fact]
    public void The_creation_event_starts_the_aggregate_and_later_events_advance_it()
    {
        var link = GuardianLink.Replay([Linked, Revoked]);

        Assert.Equal(LinkId, link.Id);
        Assert.Equal(GuardianKind.Parent, link.Kind);
        Assert.True(link.IsRevoked);
        Assert.Equal(link, GuardianLink.Rehydrate([Linked, Revoked]));
    }

    [Fact]
    public void A_stream_that_does_not_start_with_its_creation_event_throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GuardianLink.Rehydrate([Revoked]));
        Assert.Contains(nameof(GuardianRevoked), error.Message);
    }

    [Fact]
    public void A_second_creation_event_throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GuardianLink.Rehydrate([Linked, Linked]));
        Assert.Contains(nameof(GuardianLinked), error.Message);
    }
}
