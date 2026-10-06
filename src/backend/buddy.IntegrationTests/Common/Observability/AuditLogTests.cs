using System.Collections.Concurrent;

using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace buddy.IntegrationTests.Common.Observability;

// The audit-style log lines (docs/backend/observability.md) on a host of their own, so the
// captured logs only hold what these tests caused.
[Collection(BuddyApiCollection.Name)]
public sealed class AuditLogTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private readonly CapturingLoggerProvider _logs = new();

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await fixture.CreateHostAsync(
            new Dictionary<string, string?>(),
            services => services.AddSingleton<ILoggerProvider>(_logs));
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task A_first_sign_in_is_logged_and_a_call_before_it_is_rejected_with_a_log()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(403);
        });

        Assert.Contains(_logs.Entries, e => e.EventId == 1006 && Equals(e.Property("Path"), "/users/me/children"));

        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(200);
        });

        var userId = await fixture.GetUserIdAsync(token);
        Assert.Single(_logs.Entries, e => e.EventId == 1001 && Equals(e.Property("UserId"), userId));
    }

    [Fact]
    public async Task Creating_a_child_and_inviting_a_guardian_logs_ids_but_no_personal_data()
    {
        var user = await fixture.CreateUserAsync();
        var guardianToken = await fixture.GetAccessTokenAsync(user);
        var guardianId = await ProvisionAsync(guardianToken);
        var (invitee, _, _) = await fixture.CreateAuthenticatedUserAsync();

        var childResponse = await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { GivenName = "Quillon", FamilyName = "Vexmoor", Username = $"child.{Guid.CreateVersion7():N}" }).ToUrl("/users/me/children/");
            _.StatusCodeShouldBe(200);
        });
        var child = childResponse.ReadAsJson<ChildResponseDto>();

        var inviteResponse = await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Email = invitee.Email, Kind = GuardianKind.Parent }).ToUrl($"/users/me/children/{child.Id}/guardian-invites");
            _.StatusCodeShouldBe(200);
        });
        var invite = inviteResponse.ReadAsJson<GuardianInviteResponseDto>();

        Assert.Contains(_logs.Entries, e => e.EventId == 2001
            && Equals(e.Property("ChildId"), child.Id)
            && Equals(e.Property("GuardianId"), guardianId));

        Assert.Contains(_logs.Entries, e => e.EventId == 2002
            && Equals(e.Property("InviteId"), invite.Id)
            && Equals(e.Property("ChildId"), child.Id)
            && Equals(e.Property("UserId"), guardianId));

        Assert.Contains(_logs.Entries, e => e.EventId == 7001 && Equals(e.Property("EmailKind"), "guardian-invite"));

        string[] personalData = [invitee.Email, user.Email, "Quillon", "Vexmoor", child.Username, child.TemporaryPassword];
        var leaks = _logs.Entries
            .Where(e => personalData.Any(value => e.Message.Contains(value, StringComparison.OrdinalIgnoreCase)))
            .Select(e => $"{e.Category}[{e.EventId}]: {e.Message}")
            .ToArray();
        Assert.True(leaks.Length == 0, $"Logs contain personal data:\n{string.Join("\n", leaks)}");
    }

    private async Task<Guid> ProvisionAsync(string token)
    {
        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(200);
        });

        return await fixture.GetUserIdAsync(token);
    }

    private sealed record LogEntry(string Category, int EventId, LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State)
    {
        public object? Property(string name) => State.FirstOrDefault(p => p.Key == name).Value;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public LogEntry[] Entries => [.. _entries];

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
                entries.Enqueue(new LogEntry(category, eventId.Id, logLevel, message, properties));
            }
        }
    }
}
