using System.Text.Json.Serialization;

using buddy.Serialization;

namespace buddy.Features.NightDuty;

// Where a planned night came from: the fixed schedule in effect, or a per-date override.
public enum NightSource
{
    Pattern,
    Override
}

// One resolved night for ListNights: the night of Date runs into the next morning. Babysitter names
// are resolved inline, so the print sheet and the child's card need one call.
public sealed record Night(DateOnly Date, NightStatus Status);

// What a child's night resolves to. Unplanned: no override and no entry in the version in effect.
// Cleared: an override to "nothing planned". Planned: the schedule's or an override's entry.
// Scheduled is what the fixed schedule says for an overridden night (null when it says nothing, or
// when the night isn't overridden), so the editor can show "Schedule: Dad". Wire shape
// { "kind": 0 } | { "kind": 1, "scheduled" } | { "kind": 2, "entry", "source", "scheduled" }.
[JsonConverter(typeof(NightStatusJsonConverter))]
public abstract record NightStatus
{
    public sealed record NightUnplanned : NightStatus;

    public sealed record NightCleared(NightEntryDto? Scheduled) : NightStatus;

    public sealed record NightPlanned(NightEntryDto Entry, NightSource Source, NightEntryDto? Scheduled) : NightStatus;
}

public sealed class NightStatusJsonConverter : KindDiscriminatedJsonConverter<NightStatus>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [0] = typeof(NightStatus.NightUnplanned),
        [1] = typeof(NightStatus.NightCleared),
        [2] = typeof(NightStatus.NightPlanned),
    };
}
