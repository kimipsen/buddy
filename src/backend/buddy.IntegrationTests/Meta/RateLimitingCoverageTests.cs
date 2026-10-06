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
    // Container probes hit it constantly; throttling it would make a healthy replica look dead.
    private static readonly string[] ExemptRoutes = ["/health"];

    // Anonymous endpoints where the global per-IP bucket is enough: /version is tiny, and a shared
    // sleep diary is opened by a person, not polled.
    private static readonly string[] AnonymousOnGlobalLimitOnly = ["GetVersion", "GetSharedSleepDiary"];

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
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? Route(e))
            .Except(AnonymousOnGlobalLimitOnly)
            .Order()
            .ToArray();
        Assert.True(missing.Length == 0, $"These anonymous endpoints have no .RequireRateLimiting(...) policy and aren't in AnonymousOnGlobalLimitOnly: {string.Join(", ", missing)}");

        var anonymousNames = anonymous.Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName).ToHashSet();
        var stale = AnonymousOnGlobalLimitOnly.Where(name => !anonymousNames.Contains(name)).ToArray();
        Assert.True(stale.Length == 0, $"These AnonymousOnGlobalLimitOnly names aren't mapped anonymous endpoints: {string.Join(", ", stale)}");
    }

    private RouteEndpoint[] RouteEndpoints() =>
        [.. fixture.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

    private static string Route(RouteEndpoint endpoint) => "/" + endpoint.RoutePattern.RawText!.TrimStart('/');
}
