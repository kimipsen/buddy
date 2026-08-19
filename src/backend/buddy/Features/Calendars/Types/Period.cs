using System.Text.Json.Serialization;

namespace buddy.Features.Calendars;

public sealed record Period
{
    public StartsAt StartsAt { get; }

    public EndsAt EndsAt { get; }

    // JsonConstructor lets System.Text.Json (Marten's event serializer) use this constructor to
    // deserialize a Period read back from the event store without re-running TryCreate's check --
    // correctly so, since a persisted Period was already validated at write time.
    [JsonConstructor]
    private Period(StartsAt startsAt, EndsAt endsAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    // The only way to construct a Period from new input -- guarantees StartsAt is always before
    // EndsAt for every instance that exists anywhere in the system.
    public static bool TryCreate(StartsAt startsAt, EndsAt endsAt, out Period? period)
    {
        if (endsAt.Date.ToDateTime(endsAt.Time) <= startsAt.Date.ToDateTime(startsAt.Time))
        {
            period = null;
            return false;
        }

        period = new Period(startsAt, endsAt);
        return true;
    }
}
