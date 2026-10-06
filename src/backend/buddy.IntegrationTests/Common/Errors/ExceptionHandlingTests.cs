using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;

using Alba;

using buddy.Common;
using buddy.Common.Errors;
using buddy.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Npgsql;

using Xunit;

namespace buddy.IntegrationTests.Common.Errors;

// A host whose guardian-link store throws whatever the test sets, so GET /users/me/children (whose
// handler is that store's first caller) fails the way a real bug or outage would.
[Collection(BuddyApiCollection.Name)]
public sealed class ExceptionHandlingTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private const string AllowedOrigin = "https://app.errors.test";
    private const string SecretDetail = "connection string Password=hunter2";
    private const string MiddlewareCategory = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    private readonly ConcurrentQueue<(string Category, LogLevel Level, Exception? Exception)> _logs = new();

    private IAlbaHost _host = null!;
    private Exception _nextException = new InvalidOperationException(SecretDetail);

    public async Task InitializeAsync()
    {
        _host = await fixture.CreateHostAsync(
            new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin },
            services =>
            {
                var store = DispatchProxy.Create<IGuardianLinkEventStore, ThrowingProxy>();
                ((ThrowingProxy)(object)store).Next = () => _nextException;
                services.Replace(ServiceDescriptor.Singleton(store));
                services.AddSingleton<ILoggerProvider>(new CapturingLoggerProvider(_logs));
            });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task An_unexpected_exception_is_a_500_envelope_without_internals()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(500);
            _.ContentTypeShouldBe("application/json; charset=utf-8");
        });

        var body = await response.ReadAsTextAsync();
        var envelope = response.ReadAsJson<ErrorEnvelope>();

        Assert.Equal(ExceptionHandlingFeature.InternalErrorCode, envelope.Code);
        Assert.False(string.IsNullOrWhiteSpace(envelope.RequestId));
        Assert.Empty(envelope.Details);
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
    }

    [Fact]
    public async Task The_exception_is_logged_as_an_error_by_the_exception_handler()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var thrown = new InvalidOperationException("logged once");
        _nextException = thrown;

        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(500);
        });

        // The middleware logs after the handler has written the response, so it can land just
        // after the test has the response in hand.
        var entry = await WaitForAsync(e => e.Category == MiddlewareCategory && ReferenceEquals(e.Exception, thrown));
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public async Task An_unreachable_database_is_a_503_with_retry_after()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        _nextException = new NpgsqlException("Failed to connect", new SocketException((int)SocketError.ConnectionRefused));

        var response = await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(503);
            _.Header("Retry-After").SingleValueShouldEqual("5");
        });

        Assert.Equal(ExceptionHandlingFeature.DependencyUnavailableCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task An_error_response_stays_readable_by_the_frontend()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Origin", AllowedOrigin);
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(500);
            _.Header("Access-Control-Allow-Origin").SingleValueShouldEqual(AllowedOrigin);
        });
    }

    public static TheoryData<Exception, bool> Exceptions => new()
    {
        { new NpgsqlException("refused", new SocketException((int)SocketError.ConnectionRefused)), true },
        { new NpgsqlException("timeout", new TimeoutException()), true },
        { new InvalidOperationException("wrapped", new NpgsqlException("refused", new SocketException())), true },
        { new HttpRequestException(HttpRequestError.ConnectionError, "Keycloak down"), true },
        { new HttpRequestException(HttpRequestError.NameResolutionError, "no such host"), true },
        { new IOException("SMTP", new SocketException((int)SocketError.HostUnreachable)), true },
        { new PostgresException("duplicate key", "ERROR", "ERROR", "23505"), false },
        { new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden), false },
        { new InvalidOperationException("bug"), false },
        { new NullReferenceException(), false }
    };

    [Theory]
    [MemberData(nameof(Exceptions))]
    public void Only_an_unreachable_dependency_counts_as_unavailable(Exception exception, bool expected)
    {
        Assert.Equal(expected, ExceptionHandlingFeature.IsDependencyUnavailable(exception));
    }

    private async Task<(string Category, LogLevel Level, Exception? Exception)> WaitForAsync(
        Func<(string Category, LogLevel Level, Exception? Exception), bool> predicate)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (_logs.Where(predicate).ToArray() is [var entry])
            {
                return entry;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException("Expected exactly one matching log entry.");
    }

    // Every IGuardianLinkEventStore method throws the test's current exception.
    public class ThrowingProxy : DispatchProxy
    {
        public Func<Exception> Next { get; set; } = () => new InvalidOperationException();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw Next();
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<(string Category, LogLevel Level, Exception? Exception)> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, logs);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<(string, LogLevel, Exception?)> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                logs.Enqueue((category, logLevel, exception));
        }
    }
}
