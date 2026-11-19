using buddy.Common.Time;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace buddy.IntegrationTests.Common.Time;

// The Demo:Now server clock override: Development only, starts at the configured instant and
// advances with real time. Exercised on the factory, not a whole host.
public sealed class DemoClockTests
{
    private const string DemoNow = "2026-11-11T10:30:00+01:00";

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "buddy";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? demoNow) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(demoNow is null ? [] : new Dictionary<string, string?> { ["Demo:Now"] = demoNow })
            .Build();

    [Fact]
    public void Without_Demo_Now_the_system_clock_is_used_in_every_environment()
    {
        Assert.Same(TimeProvider.System, DemoClock.Create(Config(null), new Env("Development")));
        Assert.Same(TimeProvider.System, DemoClock.Create(Config(null), new Env("Production")));
    }

    [Fact]
    public void In_Development_the_clock_starts_at_Demo_Now_and_advances_with_real_time()
    {
        var real = new ManualClock(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

        var clock = DemoClock.Create(Config(DemoNow), new Env("Development"), real);

        Assert.Equal(DateTimeOffset.Parse(DemoNow, System.Globalization.CultureInfo.InvariantCulture), clock.GetUtcNow());

        real.Now += TimeSpan.FromMinutes(90);

        Assert.Equal(new DateTimeOffset(2026, 11, 11, 11, 0, 0, TimeSpan.Zero), clock.GetUtcNow());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Startup_fails_when_Demo_Now_is_set_outside_Development(string environment)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DemoClock.Create(Config(DemoNow), new Env(environment)));

        Assert.Contains("development-only", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-11-11")]
    [InlineData("2026-11-11T10:30:00")]
    [InlineData("not a date")]
    public void A_Demo_Now_without_an_offset_or_that_is_not_an_instant_is_rejected(string value)
    {
        Assert.Throws<InvalidOperationException>(() => DemoClock.Create(Config(value), new Env("Development")));
    }
}
