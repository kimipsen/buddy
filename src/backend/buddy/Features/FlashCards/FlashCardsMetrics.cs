using System.Diagnostics.Metrics;

namespace buddy.Features.FlashCards;

// Created through IMeterFactory so DI and Meter.Scope work. The meter name is listed in
// ObservabilityFeature's Sources, or the exporter would never see it. Tags are the outcome only:
// never a child, set or game id (unbounded).
public sealed class FlashCardsMetrics
{
    public const string MeterName = "Buddy.FlashCards";

    public const string FinishCounterName = "flashcards.finish";

    public const string RecoveryCounterName = "flashcards.finish.recovery";

    public const string OutcomeTag = "outcome";

    private readonly Counter<long> _finish;
    private readonly Counter<long> _recovery;

    public FlashCardsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _finish = meter.CreateCounter<long>(FinishCounterName, description: "Finished flash-card games by outcome: awarded, zero, replay or recovered.");
        _recovery = meter.CreateCounter<long>(RecoveryCounterName, description: "Finishes that wrote the history entry for stars an earlier attempt had already awarded.");
    }

    public void Finished(FlashCardFinishOutcome outcome)
    {
        _finish.Add(1, new KeyValuePair<string, object?>(OutcomeTag, outcome.Tag()));

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
