using System.Collections.Concurrent;

using Alba;

using buddy.Common.Observability;
using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Features.SleepDiaries;
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

    [Fact]
    public async Task Every_read_of_a_childs_medicines_is_logged_with_the_reader_and_the_access_path()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Quillon", "Vexmoor");
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, guardianToken, child.Id, new CreateMedicineScheduleOptions(Name: "Zorbitrex"));
        var groupId = await MedicineTestHelpers.ShareWithNewGroupAsync(fixture, guardianToken, child.Id);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var childId = child.Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var days = $"from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}";

        await GetAsync(guardianToken, $"/medicines/children/{childId}/schedules");
        await GetAsync(guardianToken, $"/medicines/groups/{groupId}/children/{childId}/schedules");
        await GetAsync(guardianToken, $"/medicines/children/{childId}/doses?{days}");
        await GetAsync(childToken, $"/medicines/children/{childId}/doses?{days}");
        await GetAsync(guardianToken, $"/medicines/groups/{groupId}/children/{childId}/doses?{days}");

        var reads = _logs.Entries.Where(e => Equals(e.Property("ChildId"), childId)).ToArray();
        AssertRead(reads, 9001, guardianId, HealthDataAccessPath.Guardian, null);
        AssertRead(reads, 9001, guardianId, HealthDataAccessPath.Group, groupId);
        AssertRead(reads, 9002, guardianId, HealthDataAccessPath.Guardian, null);
        AssertRead(reads, 9002, childId, HealthDataAccessPath.Self, null);
        AssertRead(reads, 9002, guardianId, HealthDataAccessPath.Group, groupId);
        Assert.All(reads, e => Assert.DoesNotContain("Zorbitrex", e.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_denied_read_of_a_childs_medicines_is_not_logged_as_a_read()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Quillon", "Vexmoor");

        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Get.Url($"/medicines/children/{child.Id}/schedules");
            _.StatusCodeShouldBe(404);
        });

        Assert.DoesNotContain(_logs.Entries, e => e.EventId == 9001 && Equals(e.Property("ChildId"), child.Id));
    }

    [Fact]
    public async Task Every_read_of_a_childs_sleep_diary_is_logged_with_the_reader_and_the_range()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Quillon", "Vexmoor");
        var night = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await SleepDiaryTestHelpers.LogAsync(fixture, guardianToken, child.Id, night, SleepDiaryTestHelpers.FullNight("Woke at Zorbitrex o'clock"));

        await GetAsync(guardianToken, $"/sleep-diary/children/{child.Id}/entries?from={night.AddDays(-6):yyyy-MM-dd}&to={night:yyyy-MM-dd}");

        var read = Assert.Single(_logs.Entries, e => e.EventId == 5004 && Equals(e.Property("ChildId"), child.Id));
        Assert.Equal(guardianId, read.Property("UserId"));
        Assert.Equal(HealthDataAccessPath.Guardian, read.Property("AccessPath"));
        Assert.Equal(night.AddDays(-6), read.Property("From"));
        Assert.Equal(night, read.Property("To"));
        Assert.DoesNotContain("Zorbitrex", read.Message, StringComparison.Ordinal);
    }

    private static void AssertRead(LogEntry[] reads, int eventId, Guid userId, HealthDataAccessPath accessPath, Guid? groupId) =>
        Assert.Single(reads, e => e.EventId == eventId
            && Equals(e.Property("UserId"), userId)
            && Equals(e.Property("AccessPath"), accessPath)
            && Equals(e.Property("GroupId"), groupId));

    private async Task GetAsync(string token, string url) =>
        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url(url);
            _.StatusCodeShouldBe(200);
        });

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
