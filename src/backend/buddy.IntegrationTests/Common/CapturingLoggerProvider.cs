using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace buddy.IntegrationTests.Common;

// One captured log call. State holds the structured properties of the log message.
public sealed record LogEntry(
    string Category,
    int EventId,
    LogLevel Level,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    Exception? Exception = null)
{
    public object? Property(string name) => State.FirstOrDefault(p => p.Key == name).Value;
}

// Records every log call so a test can assert on what the host logged. Meant for a host a test class
// shares through BuddyApiFixture.SharedHostAsync: keep the instance in a static field and Clear() it
// in InitializeAsync. That is safe because the whole suite is one serial collection (BuddyApiCollection).
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public LogEntry[] Entries => [.. _entries];

    public void Clear() => _entries.Clear();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            var message = formatter(state, exception) + (exception is null ? "" : $" {exception}");
            entries.Enqueue(new LogEntry(category, eventId.Id, logLevel, message, properties, exception));
        }
    }
}
