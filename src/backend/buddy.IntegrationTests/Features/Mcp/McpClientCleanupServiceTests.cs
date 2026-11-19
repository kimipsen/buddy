using buddy.Common.Configuration;
using buddy.Features.Mcp;
using buddy.IntegrationTests.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// The cleanup job's wiring, without containers: when it is registered, its options and their defaults, and its
// timer. The rules of a pass are McpClientCleanerTests.
public sealed class McpClientCleanupServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build();

    [Theory]
    [InlineData("true", "true", true, false)]
    [InlineData("true", "false", false, true)]
    [InlineData("false", "true", false, false)]
    [InlineData("false", "false", false, false)]
    public async Task The_job_is_registered_only_with_the_feature_and_the_cleanup_on_and_the_off_line_is_logged_once_otherwise(
        string mcp, string cleanup, bool expectJob, bool expectDisabledLine)
    {
        var logs = new CapturingLoggerProvider();
        var configuration = Config(("Features:Mcp", mcp), ("Mcp:ClientCleanup:Enabled", cleanup));

        // Program.cs registers the feature only when the flag is on; this builds the same container in a generic host.
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(configuration)
                    .AddSingleton<TimeProvider>(new FakeTimeProvider())
                    .AddLogging(logging => logging.AddProvider(logs));
                if (McpFeature.IsEnabled(configuration))
                {
                    services.AddMcpFeature(configuration);
                }
            })
            .Build();

        var hosted = host.Services.GetServices<IHostedService>().ToArray();
        Assert.Equal(expectJob, hosted.OfType<McpClientCleanupService>().Any());
        Assert.Equal(expectDisabledLine, hosted.Any(service => service.GetType() == typeof(McpStartupLogger)));

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(expectDisabledLine ? 1 : 0, logs.Entries.Count(entry => entry.EventId == 15007));
    }

    // The defaults are the failsafe, so a changed default has to fail a test: every other cleanup test sets its
    // options explicitly.
    [Fact]
    public void The_defaults_are_on_dry_run_and_100_deletions_per_pass()
    {
        var options = new ServiceCollection()
            .AddSingleton(Config())
            .AddOptions<McpClientCleanupOptions>().BindConfiguration("Mcp:ClientCleanup").Services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<McpClientCleanupOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.True(options.DryRun);
        Assert.Equal(100, options.MaxDeletesPerPass);
        Assert.True(McpClientCleanupOptions.IsEnabled(Config()));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("many")]
    public async Task A_cap_that_is_negative_or_does_not_parse_stops_the_host_at_startup(string cap)
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Mcp:ClientCleanup:MaxDeletesPerPass"] = cap }))
            .ConfigureServices((context, services) => services.AddValidatedMcpCleanupOptions())
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());
    }

    [Fact]
    public async Task The_first_pass_is_after_24_hours_and_the_loop_ends_when_the_service_stops()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var armed = new ArmedTimeProvider(clock);
        var registry = new CountingRegistry();
        await using var provider = new ServiceCollection()
            .AddSingleton<IKeycloakClientRegistry>(registry)
            .AddSingleton<TimeProvider>(armed)
            .AddSingleton(Options.Create(new McpClientCleanupOptions()))
            .AddLogging()
            .AddTransient<McpClientCleaner>()
            .BuildServiceProvider();
        using var service = new McpClientCleanupService(provider.GetRequiredService<IServiceScopeFactory>(), armed, provider.GetRequiredService<ILogger<McpClientCleanupService>>());

        await service.StartAsync(CancellationToken.None);
        await armed.Armed.WaitAsync(Timeout);

        // No pass at startup, and none a tick before the interval.
        Assert.Equal(0, registry.Passes);
        clock.Advance(McpClientCleanupService.Interval - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, registry.Passes);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await registry.FirstPass.WaitAsync(Timeout);
        Assert.Equal(1, registry.Passes);

        await service.StopAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(Timeout);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    private sealed class CountingRegistry : IKeycloakClientRegistry
    {
        private readonly TaskCompletionSource _firstPass = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Passes { get; private set; }

        public Task FirstPass => _firstPass.Task;

        public Task<IReadOnlyList<KeycloakClientSummary>> ListClientsAsync(CancellationToken cancellationToken)
        {
            Passes++;
            _firstPass.TrySetResult();
            return Task.FromResult<IReadOnlyList<KeycloakClientSummary>>([]);
        }

        public Task<int> CountUsersAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<IReadOnlyList<string>> ListUserIdsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> ListConsentedClientIdsAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetClientAttributeAsync(string id, string name, string value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteClientAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

internal static class McpCleanupTestRegistrations
{
    public static IServiceCollection AddValidatedMcpCleanupOptions(this IServiceCollection services)
    {
        services.AddValidatedOptions<McpClientCleanupOptions>(McpClientCleanupOptions.SectionName);
        return services;
    }
}
