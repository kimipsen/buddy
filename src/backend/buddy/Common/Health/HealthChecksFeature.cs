using System.Text.Json;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace buddy.Common.Health;

// Two probes, both anonymous and exempt from rate limiting (probes poll them constantly):
//   /health        liveness. No checks: the process is up and serving. A restart can't fix a
//                  database outage, so a dependency failure must never fail this one.
//   /health/ready  readiness. Checks Postgres (Unhealthy -> 503) and Keycloak (Degraded -> still
//                  200): without Postgres this replica can't answer anything, but JwtBearer caches
//                  the signing keys, so a Keycloak blip mostly affects new logins and shouldn't
//                  take every replica out of rotation at once.
// The body lists each check's status only, never exception text: it's public.
public static class HealthChecksFeature
{
    public const string LivenessPath = "/health";
    public const string ReadinessPath = "/health/ready";

    private const string ReadyTag = "ready";

    // Below the probes' own 5s timeout (deploy/docker-compose.prod.yml), so a hung dependency
    // reports as failed instead of the probe timing out without a body.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    public static IServiceCollection AddHealthChecksFeature(this IServiceCollection services)
    {
        services.AddHttpClient(KeycloakHealthCheck.HttpClientName);

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", HealthStatus.Unhealthy, [ReadyTag], CheckTimeout)
            .AddCheck<KeycloakHealthCheck>("keycloak", HealthStatus.Degraded, [ReadyTag], CheckTimeout);

        return services;
    }

    public static IEndpointRouteBuilder MapHealthChecksFeature(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = _ => false })
            .DisableRateLimiting();

        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReportAsync
        }).DisableRateLimiting();

        return app;
    }

    private static Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body), context.RequestAborted);
    }
}
