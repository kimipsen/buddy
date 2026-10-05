using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public union BabysitterEvent(
    BabysitterListStarted,
    BabysitterAdded,
    BabysitterDetailsChanged,
    BabysitterArchived
)
{
    public static BabysitterEvent FromPayload(object payload) => payload switch
    {
        BabysitterListStarted e => e,
        BabysitterAdded e => e,
        BabysitterDetailsChanged e => e,
        BabysitterArchived e => e,
        _ => throw new ArgumentException($"Unknown babysitter event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        BabysitterListStarted => nameof(BabysitterListStarted),
        BabysitterAdded => nameof(BabysitterAdded),
        BabysitterDetailsChanged => nameof(BabysitterDetailsChanged),
        BabysitterArchived => nameof(BabysitterArchived),
    };
}

// None of these carry a ModifiedBy: the only possible writer is the stream's own guardian (every
// write route is a /me route), so it would always equal GuardianId -- same as WorkLocationEvent.

// Appended lazily together with the first BabysitterAdded, never on its own.
public sealed record BabysitterListStarted(BabysitterListId Id, UserId GuardianId, DateTimeOffset OccurredAt);

public sealed record BabysitterAdded(BabysitterListId Id, BabysitterId BabysitterId, string Name, string ContactInfo, DateTimeOffset OccurredAt);

public sealed record BabysitterDetailsChanged(BabysitterListId Id, BabysitterId BabysitterId, BabysitterDetails Before, BabysitterDetails After, DateTimeOffset OccurredAt);

public sealed record BabysitterArchived(BabysitterListId Id, BabysitterId BabysitterId, DateTimeOffset OccurredAt);
