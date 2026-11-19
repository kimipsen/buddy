using System.Globalization;
using System.Text.RegularExpressions;

namespace buddy.Common.Time;

// Development-only server clock for the documentation screenshots: with `Demo:Now` (env
// `Demo__Now`, an ISO-8601 instant with an offset) set, the API's one TimeProvider starts at that
// instant and then advances with real elapsed time, so "today" on the server is the demo day.
// Only the TimeProvider moves: token validation (JWT bearer) keeps its own real clock.
public static partial class DemoClock
{
    public const string ConfigurationKey = "Demo:Now";

    // The provider the API registers: the offset clock when Demo:Now is set (Development only),
    // otherwise the system clock. `reference` is the clock the offset advances with.
    public static TimeProvider Create(IConfiguration configuration, IHostEnvironment environment, TimeProvider? reference = null)
    {
        var raw = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(raw))
        {
            return TimeProvider.System;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} (Demo__Now) is a development-only server clock override and must not be set in the '{environment.EnvironmentName}' environment.");
        }

        if (!HasOffset(raw)
            || !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be an ISO-8601 instant with an offset, such as 2026-11-11T10:30:00+01:00; got '{raw}'.");
        }

        return new OffsetTimeProvider(start, reference ?? TimeProvider.System);
    }

    private static bool HasOffset(string raw) =>
        raw.Contains('T', StringComparison.Ordinal) && (raw.EndsWith('Z') || OffsetSuffix().IsMatch(raw));

    [GeneratedRegex(@"[+-]\d{2}:\d{2}$")]
    private static partial Regex OffsetSuffix();
}

// Starts at `start` and advances with the real time that passes on `reference`.
public sealed class OffsetTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _start;
    private readonly TimeProvider _reference;
    private readonly DateTimeOffset _referenceStart;

    public OffsetTimeProvider(DateTimeOffset start, TimeProvider reference)
    {
        _start = start;
        _reference = reference;
        _referenceStart = reference.GetUtcNow();
    }

    public override DateTimeOffset GetUtcNow() => _start + (_reference.GetUtcNow() - _referenceStart);

    public override long GetTimestamp() => _reference.GetTimestamp();

    public override long TimestampFrequency => _reference.TimestampFrequency;
}

// Logs a warning at startup whenever the demo clock override is active.
internal sealed class DemoClockWarning(TimeProvider time, ILogger<DemoClockWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.DemoClockActive(time.GetUtcNow());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
