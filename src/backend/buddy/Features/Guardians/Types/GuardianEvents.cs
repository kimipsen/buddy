using buddy.Features.Users;

namespace buddy.Features.Guardians;

public union GuardianEvent(
    GuardianLinked,
    GuardianKindChanged,
    GuardianRevoked
)
{
    public static GuardianEvent FromPayload(object payload) => payload switch
    {
        GuardianLinked e => e,
        GuardianKindChanged e => e,
        GuardianRevoked e => e,
        _ => throw new ArgumentException($"Unknown guardian event payload: {payload.GetType().Name}", nameof(payload)),
    };

    // Persistence/API discriminator. A union is a value type, so GetType().Name on a boxed
    // GuardianEvent returns "GuardianEvent" for every case -- use this instead.
    public string EventType => this switch
    {
        GuardianLinked => nameof(GuardianLinked),
        GuardianKindChanged => nameof(GuardianKindChanged),
        GuardianRevoked => nameof(GuardianRevoked),
    };
}

public sealed record GuardianLinked(GuardianLinkId GuardianLinkId, UserId ChildId, UserId GuardianId, GuardianKind Kind, DateTimeOffset OccurredAt);

// Kind is a record-keeping label only (see GuardianKind) -- changing it never affects the
// CalendarRole.Owner default a guardian already gets.
public sealed record GuardianKindChanged(GuardianLinkId GuardianLinkId, GuardianKind Before, GuardianKind After, DateTimeOffset OccurredAt);

public sealed record GuardianRevoked(GuardianLinkId GuardianLinkId, DateTimeOffset OccurredAt);
