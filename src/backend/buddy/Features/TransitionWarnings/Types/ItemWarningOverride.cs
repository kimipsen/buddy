using System.Collections.Immutable;
using System.Text.Json.Serialization;

using buddy.Serialization;

namespace buddy.Features.TransitionWarnings;

// One calendar item's exception to a child's defaults (Question 4): never warn, or warn at its own
// lead times. Stored as { "kind": 0 } or { "kind": 1, "leadMinutes": [...] }.
[JsonConverter(typeof(ItemWarningOverrideJsonConverter))]
public abstract record ItemWarningOverride
{
    public sealed record Off : ItemWarningOverride;

    public sealed record LeadTimes(ImmutableArray<int> LeadMinutes) : ItemWarningOverride
    {
        // ImmutableArray compares by reference; an identical override is a no-op.
        public bool Equals(LeadTimes? other) => other is not null && LeadMinutes.SequenceEqual(other.LeadMinutes);

        public override int GetHashCode() => LeadMinutes.Length;
    }

    // An empty list means "never warn for this item", the shape the API takes and returns.
    public static ItemWarningOverride FromLeadMinutes(IEnumerable<int> leadMinutes)
    {
        var normalized = LeadTimeRules.Normalize(leadMinutes);
        return normalized.IsEmpty ? new Off() : new LeadTimes(normalized);
    }

    public ImmutableArray<int> ToLeadMinutes() => this switch
    {
        LeadTimes leadTimes => leadTimes.LeadMinutes,
        _ => [],
    };
}

public sealed class ItemWarningOverrideJsonConverter : KindDiscriminatedJsonConverter<ItemWarningOverride>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [0] = typeof(ItemWarningOverride.Off),
        [1] = typeof(ItemWarningOverride.LeadTimes),
    };
}
