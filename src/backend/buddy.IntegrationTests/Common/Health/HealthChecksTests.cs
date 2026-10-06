using System.Text.Json;

using Alba;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Common.Health;

[Collection(BuddyApiCollection.Name)]
public sealed class HealthChecksTests(BuddyApiFixture fixture)
{
    // Nothing listens on port 1, so every connection attempt is refused straight away.
    private const string UnreachablePostgres = "Host=127.0.0.1;Port=1;Database=buddy;Username=buddy;Password=unused;Timeout=2";

    [Fact]
    public async Task Readiness_is_healthy_when_postgres_and_keycloak_are_reachable()
    {
        var report = await ReadinessAsync(fixture.Host, expectedStatusCode: 200);

        Assert.Equal("Healthy", report.Status);
        Assert.Equal("Healthy", report.Checks["postgres"]);
        Assert.Equal("Healthy", report.Checks["keycloak"]);
    }

    [Fact]
    public async Task Without_postgres_readiness_fails_but_liveness_stays_up()
    {
        await using var host = await fixture.CreateHostAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = UnreachablePostgres
        });

        var report = await ReadinessAsync(host, expectedStatusCode: 503);

        Assert.Equal("Unhealthy", report.Status);
        Assert.Equal("Unhealthy", report.Checks["postgres"]);
        Assert.Equal("Healthy", report.Checks["keycloak"]);

        await host.Scenario(_ =>
        {
            _.Get.Url("/health");
            _.StatusCodeShouldBe(200);
        });
    }

    [Fact]
    public async Task Without_keycloak_readiness_is_degraded_but_still_ready()
    {
        await using var host = await fixture.CreateHostAsync(new Dictionary<string, string?>
        {
            ["Authentication:Keycloak:Authority"] = "http://127.0.0.1:1/realms/buddy"
        });

        var report = await ReadinessAsync(host, expectedStatusCode: 200);

        Assert.Equal("Degraded", report.Status);
        Assert.Equal("Healthy", report.Checks["postgres"]);
        Assert.Equal("Degraded", report.Checks["keycloak"]);
    }

    [Fact]
    public async Task The_readiness_report_never_exposes_exception_details()
    {
        await using var host = await fixture.CreateHostAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = UnreachablePostgres
        });

        var response = await host.Scenario(_ =>
        {
            _.Get.Url("/health/ready");
            _.StatusCodeShouldBe(503);
        });

        var body = await response.ReadAsTextAsync();
        Assert.DoesNotContain("127.0.0.1", body);
        Assert.DoesNotContain("Exception", body);
    }

    private static async Task<ReadinessReport> ReadinessAsync(IAlbaHost host, int expectedStatusCode)
    {
        var response = await host.Scenario(_ =>
        {
            _.Get.Url("/health/ready");
            _.StatusCodeShouldBe(expectedStatusCode);
            _.ContentTypeShouldBe("application/json");
        });

        return JsonSerializer.Deserialize<ReadinessReport>(await response.ReadAsTextAsync(), JsonSerializerOptions.Web)
            ?? throw new Xunit.Sdk.XunitException("Empty readiness report.");
    }

    private sealed record ReadinessReport(string Status, IReadOnlyDictionary<string, string> Checks);
}
