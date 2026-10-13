using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

// One child's transition warning settings (docs/backend/analysis/transition-warnings.md): the
// defaults, plus per-item overrides keyed by CalendarItemId. Who changed what lives on the events
// (ModifiedBy), which is all the history needs; nothing reads a last-modified-by here.
public sealed record TransitionWarningSettings(
    TransitionWarningSettingsId Id,
    UserId ChildId,
    WarningDefaults Defaults,
    ImmutableDictionary<CalendarItemId, ItemWarningOverride> ItemOverrides)
{
    // A child with no stream yet -- what TransitionWarningsStarted folds to, so the reader and the
    // first writer work from the same value: warnings off, 15/5/1 and every kind pre-filled.
    public static TransitionWarningSettings Initial(TransitionWarningSettingsId id, UserId childId) =>
        new(id, childId, WarningDefaults.Initial, ImmutableDictionary<CalendarItemId, ItemWarningOverride>.Empty);

    public static TransitionWarningSettings ForChild(UserId childId) => Initial(TransitionWarningSettingsId.ForChild(childId), childId);

    public static TransitionWarningSettings? Rehydrate(IEnumerable<TransitionWarningEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static TransitionWarningSettings Replay(IEnumerable<TransitionWarningEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create/Evolve: JasperFx's projection generator scans those names.
    public static TransitionWarningSettings Start(TransitionWarningEvent @event) => @event switch
    {
        TransitionWarningsStarted started => Initial(started.Id, started.ChildId),
        _ => throw EventReplay.NotAStartEvent(nameof(TransitionWarningSettings), @event.EventType),
    };

    public static TransitionWarningSettings Advance(TransitionWarningSettings settings, TransitionWarningEvent @event) => @event switch
    {
        TransitionWarningDefaultsConfigured configured => settings with { Defaults = configured.After },
        ItemWarningOverrideSet set => settings with { ItemOverrides = settings.ItemOverrides.SetItem(set.ItemId, set.After) },
        ItemWarningOverrideCleared cleared => settings with { ItemOverrides = settings.ItemOverrides.Remove(cleared.ItemId) },
        TransitionWarningsStarted => throw EventReplay.AlreadyStarted(nameof(TransitionWarningSettings), @event.EventType),
    };
}
