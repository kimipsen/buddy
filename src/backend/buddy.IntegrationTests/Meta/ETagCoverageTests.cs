using buddy.Common.Http;
using buddy.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// Every GET endpoint either opts into conditional GET (ETagMetadata, normally from its feature's
// route group) or is listed here on purpose -- so a new GET, or a new route group, can't silently
// miss an ETag or silently gain one. See docs/backend/analysis/conditional-get-etags.md.
[Collection(BuddyApiCollection.Name)]
public sealed class ETagCoverageTests(BuddyApiFixture fixture)
{
    // Infrastructure endpoints outside every feature group: /version is tiny and polled by probes,
    // and the OpenAPI document (mapped in Development only) is for tooling, not the app. /health
    // isn't listed because MapHealthChecks maps it for every method, not as a GET.
    private static readonly string[] ExcludedRoutes = ["/version", "/openapi/{documentName}.json"];

    [Fact]
    public void Every_get_endpoint_has_an_etag_or_is_explicitly_excluded()
    {
        var getEndpoints = fixture.Host.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true)
            .ToArray();

        var missing = getEndpoints
            .Where(e => e.Metadata.GetMetadata<ETagMetadata>() is null)
            .Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'))
            .Except(ExcludedRoutes)
            .Order()
            .ToArray();
        Assert.True(missing.Length == 0, $"These GET endpoints have no .WithETag() (add it to the route group) and aren't in ExcludedRoutes: {string.Join(", ", missing)}");

        var mappedRoutes = getEndpoints.Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/')).ToHashSet();
        var stale = ExcludedRoutes.Where(r => !mappedRoutes.Contains(r)).ToArray();
        Assert.True(stale.Length == 0, $"These ExcludedRoutes don't match any mapped GET endpoint (renamed or removed?): {string.Join(", ", stale)}");
    }
}
