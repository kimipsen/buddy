using System.Diagnostics.Metrics;

namespace buddy.Features.FlashCards;

// Created through IMeterFactory so DI and Meter.Scope work. The meter name is listed in
// ObservabilityFeature's Sources, or the exporter would never see it. Tags are the outcome and the
// set kind (math, text, builtin or unknown): never a child, set or game id (unbounded).
public sealed class FlashCardsMetrics
{
    public const string MeterName = "Buddy.FlashCards";

    public const string FinishCounterName = "flashcards.finish";

    public const string RecoveryCounterName = "flashcards.finish.recovery";

    public const string OutcomeTag = "outcome";

    public const string KindTag = "kind";

    private readonly Counter<long> _finish;
    private readonly Counter<long> _recovery;

    public FlashCardsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _finish = meter.CreateCounter<long>(FinishCounterName, description: "Finished flash-card games by outcome (awarded, zero, replay or recovered) and set kind (math, text, builtin or unknown).");
        _recovery = meter.CreateCounter<long>(RecoveryCounterName, description: "Finishes that wrote the history entry for stars an earlier attempt had already awarded.");
    }

    public void Finished(FlashCardFinishOutcome outcome, string kind)
    {
        _finish.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome.Tag()), new KeyValuePair<string, object?>(KindTag, kind));

        if (outcome == FlashCardFinishOutcome.Recovered)
        {
            _recovery.Add(1);
        }
    }
}

public enum FlashCardFinishOutcome
{
    Awarded,
    Zero,
    Replay,
    Recovered
}

public static class FlashCardFinishOutcomeExtensions
{
    public static string Tag(this FlashCardFinishOutcome outcome) => outcome switch
    {
        FlashCardFinishOutcome.Awarded => "awarded",
        FlashCardFinishOutcome.Zero => "zero",
        FlashCardFinishOutcome.Replay => "replay",
        FlashCardFinishOutcome.Recovered => "recovered",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown flash-card finish outcome.")
    };
}

public static class FlashCardKindTags
{
    // A replayed game carries no loaded set and the set may be gone: the kind is not known.
    public const string Unknown = "unknown";

    public static string Tag(this FlashCardSetKind kind) => kind switch
    {
        FlashCardSetKind.Math => "math",
        FlashCardSetKind.Text => "text",
        FlashCardSetKind.BuiltIn => "builtin",
        _ => Unknown
    };
}
