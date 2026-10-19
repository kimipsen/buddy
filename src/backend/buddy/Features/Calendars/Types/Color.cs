namespace buddy.Features.Calendars;

public sealed record Color(string Value)
{
    // A CSS color ("#0ea5e9"); the same cap as WorkLocationRules.MaxColorLength. The calendar item and
    // medicine endpoints trim the value before it is validated and stored.
    public const int MaxLength = 32;

    public static Color New(string value) => new(value);
}
