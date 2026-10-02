using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public union WorkLocationEvent(
    WorkLocationScheduleStarted,
    WorkLocationAdded,
    WorkLocationDetailsChanged,
    WorkLocationArchived,
    WorkPatternReplaced,
    WorkLocationOverridden,
    WorkLocationOverrideCleared
)
{
    public static WorkLocationEvent FromPayload(object payload) => payload switch
    {
        WorkLocationScheduleStarted e => e,
        WorkLocationAdded e => e,
        WorkLocationDetailsChanged e => e,
        WorkLocationArchived e => e,
        WorkPatternReplaced e => e,
        WorkLocationOverridden e => e,
        WorkLocationOverrideCleared e => e,
        _ => throw new ArgumentException($"Unknown work location event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        WorkLocationScheduleStarted => nameof(WorkLocationScheduleStarted),
        WorkLocationAdded => nameof(WorkLocationAdded),
        WorkLocationDetailsChanged => nameof(WorkLocationDetailsChanged),
        WorkLocationArchived => nameof(WorkLocationArchived),
        WorkPatternReplaced => nameof(WorkPatternReplaced),
        WorkLocationOverridden => nameof(WorkLocationOverridden),
        WorkLocationOverrideCleared => nameof(WorkLocationOverrideCleared),
    };
}

// None of these carry a ModifiedBy: the only possible writer is the stream's own guardian (every
// write route is a /me route), so it would always equal GuardianId.

// Appended lazily together with the first real write, never on its own -- same as
// PickupScheduleCreated/ProgressStarted. Seeds an empty one-week pattern anchored on the Monday of
// the week it occurred in, so WorkPatternReplaced.Before is never null.
public sealed record WorkLocationScheduleStarted(WorkLocationScheduleId Id, UserId GuardianId, DateTimeOffset OccurredAt);

public sealed record WorkLocationAdded(WorkLocationScheduleId Id, WorkLocationId LocationId, string Name, Icon Icon, Color Color, DateTimeOffset OccurredAt);

public sealed record WorkLocationDetailsChanged(WorkLocationScheduleId Id, WorkLocationId LocationId, WorkLocationDetails Before, WorkLocationDetails After, DateTimeOffset OccurredAt);

public sealed record WorkLocationArchived(WorkLocationScheduleId Id, WorkLocationId LocationId, DateTimeOffset OccurredAt);

public sealed record WorkPatternReplaced(WorkLocationScheduleId Id, WorkPattern Before, WorkPattern After, DateTimeOffset OccurredAt);

// One event per date, even when a whole range is set at once, so Before/After stay per-date like
// PickupAssigned.
public sealed record WorkLocationOverridden(WorkLocationScheduleId Id, DateOnly Date, WorkDayOverride? Before, WorkDayOverride After, DateTimeOffset OccurredAt);

public sealed record WorkLocationOverrideCleared(WorkLocationScheduleId Id, DateOnly Date, WorkDayOverride Before, DateTimeOffset OccurredAt);
