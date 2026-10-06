using buddy.Features.Users;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace buddy.Common.Health;

// Fetches the realm's OIDC discovery document from the same Authority JwtBearer uses for its
// metadata and signing keys (see UsersFeature), so "healthy" means token validation can refresh.
public sealed class KeycloakHealthCheck(IHttpClientFactory httpClientFactory, IOptionsMonitor<KeycloakOptions> keycloakOptions) : IHealthCheck
{
    public const string HttpClientName = "keycloak-health";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var discovery = $"{keycloakOptions.CurrentValue.Authority.TrimEnd('/')}/.well-known/openid-configuration";

        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(discovery, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, $"OIDC discovery returned {(int)response.StatusCode}.");
        }
        catch (HttpRequestException exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }
}
