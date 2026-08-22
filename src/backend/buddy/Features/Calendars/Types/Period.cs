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
    // EndsAt for every instance that exists anywhere in the system. The error message lives here,
    // not at each call site, so every caller reports the same wording for the same violation.
    public static PeriodValidationResult TryCreate(StartsAt startsAt, EndsAt endsAt) =>
        endsAt.Date.ToDateTime(endsAt.Time) <= startsAt.Date.ToDateTime(startsAt.Time)
            ? new PeriodValidationResult.Invalid("An event's end time must be after its start time.")
            : new PeriodValidationResult.Valid(new Period(startsAt, endsAt));
}

// Period.TryCreate only ever succeeds or fails validation -- there's no NotFound/Forbidden concept
// for constructing a value object -- so this is its own two-case type instead of the shared
// Result<T>, which would force every caller to handle two cases that can never occur here.
public union PeriodValidationResult(PeriodValidationResult.Valid, PeriodValidationResult.Invalid)
{
    public sealed record Valid(Period Period);
    public sealed record Invalid(string Message);
}
