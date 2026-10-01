namespace buddy.Common.Concurrency;

// Wolverine middleware applied to every handler (Program.cs): opens the StreamVersionTracker
// scope that turns a handler's ReadAsync -> AppendAsync into an expected-version append.
public static class StreamVersionScopeMiddleware
{
    public static IDisposable? Before() => StreamVersionTracker.BeginScope();

    public static void Finally(IDisposable? scope) => scope?.Dispose();
}
