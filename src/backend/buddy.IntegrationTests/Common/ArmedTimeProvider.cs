using Microsoft.Extensions.Time.Testing;

namespace buddy.IntegrationTests.Common;

// A FakeTimeProvider that says when a timer has been created on it. BackgroundService.ExecuteAsync creates its
// PeriodicTimer on a thread-pool thread after StartAsync returns, and a timer created after Advance starts at
// the advanced time and never fires: a test waits for Armed before it moves the clock.
public sealed class ArmedTimeProvider(FakeTimeProvider inner) : TimeProvider
{
    private readonly TaskCompletionSource _armed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Armed => _armed.Task;

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override long GetTimestamp() => inner.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = inner.CreateTimer(callback, state, dueTime, period);
        _armed.TrySetResult();
        return timer;
    }
}
