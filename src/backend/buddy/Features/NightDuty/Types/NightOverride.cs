namespace buddy.Features.NightDuty;

// A one-off exception to the fixed schedule for one date: a planned entry, or Cleared ("the
// schedule says Dad, but nothing is planned this night"). Having no override at all means "follow
// the fixed schedule". Persisted through NightOverrideJsonConverter. See
// docs/backend/analysis/night-duty.md, Question 5.
public union NightOverride(NightOverride.Planned, NightOverride.Cleared)
{
    public sealed record Planned(NightEntry Entry);

    public sealed record Cleared;

    // The planned entry, or null for Cleared. Not persisted: NightOverrideJsonConverter writes only
    // the case.
    public NightEntry? PlannedEntry => this switch
    {
        Planned planned => planned.Entry,
        Cleared => null,
    };
}
