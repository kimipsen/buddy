using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public union NightDutyEvent(
    NightScheduleStarted,
    NightPatternReplaced,
    NightOverridden,
    NightOverrideCleared
)
{
    public static NightDutyEvent FromPayload(object payload) => payload switch
    {
        NightScheduleStarted e => e,
        NightPatternReplaced e => e,
        NightOverridden e => e,
        NightOverrideCleared e => e,
        _ => throw new ArgumentException($"Unknown night duty event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        NightScheduleStarted => nameof(NightScheduleStarted),
        NightPatternReplaced => nameof(NightPatternReplaced),
        NightOverridden => nameof(NightOverridden),
        NightOverrideCleared => nameof(NightOverrideCleared),
    };
}

// Every write carries ModifiedBy: any active guardian of the child can write, so the writer isn't
// implied by the stream (unlike the /me-only work-location events). See
// docs/backend/analysis/night-duty.md, "Events".

// Appended lazily together with the first real write, never on its own -- same as
// WorkLocationScheduleStarted. Seeds a schedule with no versions and no overrides.
public sealed record NightScheduleStarted(NightScheduleId Id, UserId ChildId, DateTimeOffset OccurredAt);

// One save of the fixed schedule. Replaces every stored version whose EffectiveFrom is on or after
// Pattern.EffectiveFrom ("from this date on, this is the schedule"). The versions it replaced
// aren't carried: they are already in the stream, and nothing reads them.
public sealed record NightPatternReplaced(NightScheduleId Id, NightPattern Pattern, UserId ModifiedBy, DateTimeOffset OccurredAt);

// One event per date, even when several dates are set at once (a switch is two), like
// WorkLocationOverridden. The override replaced isn't carried.
public sealed record NightOverridden(NightScheduleId Id, DateOnly Date, NightOverride Override, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record NightOverrideCleared(NightScheduleId Id, DateOnly Date, NightOverride Before, UserId ModifiedBy, DateTimeOffset OccurredAt);
