using buddy.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// The global rate limiter covers every endpoint by default, so the decisions worth policing are the
// exceptions: an endpoint that opts out entirely, and an anonymous endpoint (no per-user bucket) that
// relies on the per-IP bucket alone instead of a named policy. A new one of either has to be listed
// here on purpose. See docs/backend/analysis/rate-limiting.md.
[Collection(BuddyApiCollection.Name)]
public sealed class RateLimitingCoverageTests(BuddyApiFixture fixture)
{
    // Container probes hit them constantly; throttling them would make a healthy replica look dead.
    private static readonly string[] ExemptRoutes = ["/health", "/health/ready"];

    // Anonymous endpoints where the global per-IP bucket is enough: /version and /features are tiny
    // and read once per app load (docs/backend/analysis/feature-flags.md), a shared
    // sleep diary is opened by a person, not polled, and the OpenAPI document is a public contract
    // (the code is open source) that each family's instance serves about itself -- see
    // docs/backend/analysis/openapi-client-contract.md. Listed by endpoint name, or by route for an
    // endpoint without one (the OpenAPI document endpoint has no name).
    private static readonly string[] AnonymousOnGlobalLimitOnly = ["GetVersion", "GetFeatures", "GetSharedSleepDiary", "/openapi/{documentName}.json"];

    [Fact]
    public void Only_listed_endpoints_opt_out_of_rate_limiting()
    {
        var endpoints = RouteEndpoints();

        var exempt = endpoints
            .Where(e => e.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null)
            .Select(Route)
            .ToHashSet();

        var unlisted = exempt.Except(ExemptRoutes).Order().ToArray();
        Assert.True(unlisted.Length == 0, $"These endpoints call .DisableRateLimiting() but aren't in ExemptRoutes: {string.Join(", ", unlisted)}");

        var stale = ExemptRoutes.Except(exempt).ToArray();
        Assert.True(stale.Length == 0, $"These ExemptRoutes aren't exempt (or no longer mapped): {string.Join(", ", stale)}");
    }

    [Fact]
    public void Every_anonymous_endpoint_has_a_named_policy_or_is_listed()
    {
        var anonymous = RouteEndpoints()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null
                && e.Metadata.GetMetadata<DisableRateLimitingAttribute>() is null)
            .ToArray();

        var missing = anonymous
            .Where(e => e.Metadata.GetMetadata<EnableRateLimitingAttribute>() is null)
            .Select(NameOrRoute)
            .Except(AnonymousOnGlobalLimitOnly)
            .Order()
            .ToArray();
        Assert.True(missing.Length == 0, $"These anonymous endpoints have no .RequireRateLimiting(...) policy and aren't in AnonymousOnGlobalLimitOnly: {string.Join(", ", missing)}");

        var anonymousKeys = anonymous.Select(NameOrRoute).ToHashSet();
        var stale = AnonymousOnGlobalLimitOnly.Where(key => !anonymousKeys.Contains(key)).ToArray();
        Assert.True(stale.Length == 0, $"These AnonymousOnGlobalLimitOnly entries aren't mapped anonymous endpoints: {string.Join(", ", stale)}");
    }

    private RouteEndpoint[] RouteEndpoints() =>
        [.. fixture.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

    private static string NameOrRoute(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? Route(endpoint);

    private static string Route(RouteEndpoint endpoint) => "/" + endpoint.RoutePattern.RawText!.TrimStart('/');
}
