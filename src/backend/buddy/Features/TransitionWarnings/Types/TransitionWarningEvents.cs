using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

public union TransitionWarningEvent(
    TransitionWarningsStarted,
    TransitionWarningDefaultsConfigured,
    ItemWarningOverrideSet,
    ItemWarningOverrideCleared
)
{
    public static TransitionWarningEvent FromPayload(object payload) => payload switch
    {
        TransitionWarningsStarted e => e,
        TransitionWarningDefaultsConfigured e => e,
        ItemWarningOverrideSet e => e,
        ItemWarningOverrideCleared e => e,
        _ => throw new ArgumentException($"Unknown transition warning event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        TransitionWarningsStarted => nameof(TransitionWarningsStarted),
        TransitionWarningDefaultsConfigured => nameof(TransitionWarningDefaultsConfigured),
        ItemWarningOverrideSet => nameof(ItemWarningOverrideSet),
        ItemWarningOverrideCleared => nameof(ItemWarningOverrideCleared),
    };
}

// Appended together with the first write; reading a missing stream returns the initial settings.
public sealed record TransitionWarningsStarted(TransitionWarningSettingsId Id, UserId ChildId, DateTimeOffset OccurredAt);

// A full replace of the defaults. An identical save appends nothing.
public sealed record TransitionWarningDefaultsConfigured(
    TransitionWarningSettingsId Id,
    WarningDefaults Before,
    WarningDefaults After,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);

// Before is null when the item had no override yet.
public sealed record ItemWarningOverrideSet(
    TransitionWarningSettingsId Id,
    CalendarItemId ItemId,
    ItemWarningOverride? Before,
    ItemWarningOverride After,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);

// Clearing a missing override appends nothing.
public sealed record ItemWarningOverrideCleared(
    TransitionWarningSettingsId Id,
    CalendarItemId ItemId,
    ItemWarningOverride Before,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);
