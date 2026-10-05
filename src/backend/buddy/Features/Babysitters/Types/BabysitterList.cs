using System.Collections.Immutable;
using System.Text.Json.Serialization;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public sealed record BabysitterList(
    BabysitterListId Id,
    UserId GuardianId,
    ImmutableList<Babysitter> Babysitters)
{
    // What a guardian who has never added anyone looks like -- the same state BabysitterListStarted
    // folds to, so handlers can treat "no stream yet" uniformly.
    public static BabysitterList Empty(UserId guardianId) => new(BabysitterListId.ForGuardian(guardianId), guardianId, []);

    public Babysitter? Find(BabysitterId id) => Babysitters.FirstOrDefault(b => b.Id == id);

    public Babysitter? FindActive(BabysitterId id) => Find(id) is { IsArchived: false } babysitter ? babysitter : null;

    // Derived, so kept out of the persisted snapshot JSON.
    [JsonIgnore]
    public IEnumerable<Babysitter> ActiveBabysitters => Babysitters.Where(b => !b.IsArchived);

    private BabysitterList With(BabysitterId id, Func<Babysitter, Babysitter> change) =>
        this with { Babysitters = Babysitters.Select(b => b.Id == id ? change(b) : b).ToImmutableList() };

    public static BabysitterList? Rehydrate(IEnumerable<BabysitterEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static BabysitterList Replay(IEnumerable<BabysitterEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create -- see WorkLocationSchedule.Start for why.
    public static BabysitterList Start(BabysitterEvent @event) => @event switch
    {
        BabysitterListStarted started => Empty(started.GuardianId),
        _ => throw EventReplay.NotAStartEvent(nameof(BabysitterList), @event.EventType)
    };

    public static BabysitterList Advance(BabysitterList list, BabysitterEvent @event) => @event switch
    {
        BabysitterAdded added => list with
        {
            Babysitters = list.Babysitters.Add(new Babysitter(added.BabysitterId, added.Name, added.ContactInfo))
        },
        BabysitterDetailsChanged changed => list.With(
            changed.BabysitterId, b => b with { Name = changed.After.Name, ContactInfo = changed.After.ContactInfo }),
        BabysitterArchived archived => list.With(archived.BabysitterId, b => b with { IsArchived = true }),
        BabysitterListStarted => throw EventReplay.AlreadyStarted(nameof(BabysitterList), @event.EventType)
    };
}
