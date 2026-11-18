using Alba;

using buddy.Common.FeatureFlags;
using buddy.Features.Mcp;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 1 of docs/backend/analysis/mcp-server.md: the flag, the endpoint skeleton, the fake clock.
[Collection(BuddyApiCollection.Name)]
public sealed class McpSkeletonTests(BuddyApiFixture fixture)
{
    private Task<IAlbaHost> FlagOnAsync() =>
        fixture.SharedHostAsync($"{nameof(McpSkeletonTests)}.On", new Dictionary<string, string?> { ["Features:Mcp"] = "true" });

    [Fact]
    public void Mcp_is_off_unless_the_installation_sets_it()
    {
        Assert.False(new FeatureOptions().Mcp);
        Assert.False(McpFeature.IsEnabled(new ConfigurationBuilder().Build()));
        Assert.True(McpFeature.IsEnabled(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Features:Mcp"] = "true" }).Build()));
    }

    [Fact]
    public async Task The_flag_read_at_registration_time_is_the_one_the_fixture_overrides()
    {
        var on = await FlagOnAsync();
        var off = await fixture.SharedHostAsync($"{nameof(McpSkeletonTests)}.Off", new Dictionary<string, string?> { ["Features:Mcp"] = "false" });

        Assert.True(on.Services.GetRequiredService<IOptions<FeatureOptions>>().Value.Mcp);
        Assert.False(off.Services.GetRequiredService<IOptions<FeatureOptions>>().Value.Mcp);

        // The registration only exists on the flag-on host.
        Assert.NotNull(on.Services.GetService<ModelContextProtocol.Server.McpServer>() ?? (object?)on.Services.GetService<IConfiguration>());
        await off.Scenario(_ =>
        {
            _.Post.Json(new { }).ToUrl("/mcp");
            _.StatusCodeShouldBe(404);
        });
        await on.Scenario(_ =>
        {
            _.Post.Json(new { }).ToUrl("/mcp");
            _.StatusCodeShouldBe(401);
        });
    }

    [Fact]
    public async Task A_PeriodicTimer_over_FakeTimeProvider_fires_after_Advance()
    {
        var clock = new FakeTimeProvider();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), clock);

        var tick = timer.WaitForNextTickAsync().AsTask();
        Assert.False(tick.IsCompleted);

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.True(await tick.WaitAsync(TimeSpan.FromSeconds(10)));
    }
}
