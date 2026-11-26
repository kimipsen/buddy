using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using buddy.Features.FlashCards;

namespace buddy.IntegrationTests.Features.FlashCards;

// A per-test MeterListener on the Buddy.FlashCards meter. The whole suite shares one host and so
// one meter, and a child id cannot be a tag, so isolation is the delta from this listener's own
// start: a test asserts how much each outcome moved since it began, never an absolute count.
internal sealed class FlashCardMeterDeltas : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<string, long> _finish = new();
    private long _recovery;

    public FlashCardMeterDeltas()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == FlashCardsMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == FlashCardsMetrics.RecoveryCounterName)
            {
                Interlocked.Add(ref _recovery, measurement);
                return;
            }

            foreach (var tag in tags)
            {
                if (tag.Key == FlashCardsMetrics.OutcomeTag && tag.Value is string outcome)
                {
                    _finish.AddOrUpdate(outcome, measurement, (_, current) => current + measurement);
                }
            }
        });

        _listener.Start();
    }

    public long Finish(FlashCardFinishOutcome outcome) => _finish.GetValueOrDefault(outcome.Tag());

    public long Recovery => Interlocked.Read(ref _recovery);

    public void Dispose() => _listener.Dispose();
}
