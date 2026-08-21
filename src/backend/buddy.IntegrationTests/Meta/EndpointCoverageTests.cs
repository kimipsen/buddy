using System.Reflection;

using buddy.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// The drift guard from docs/backend/analysis/integration-testing-strategy.md: every endpoint
// mapped with .WithName(...) in the main project must have at least one test method carrying a
// matching [CoversEndpoint("...")] attribute. A new endpoint shipped without a test fails this
// with the missing name in the assertion message, instead of relying on a reviewer to notice a
// gap in Features/<Feature>/<Command>/<Command>Tests.cs.
[Collection(BuddyApiCollection.Name)]
public sealed class EndpointCoverageTests(BuddyApiFixture fixture)
{
    [Fact]
    public void Every_mapped_endpoint_has_a_matching_test()
    {
        var mappedEndpointNames = fixture.Host.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .Select(e => e.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName)
            .OfType<string>()
            .ToHashSet();

        var coveredEndpointNames = typeof(EndpointCoverageTests).Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(m => m.GetCustomAttributes<CoversEndpointAttribute>())
            .Select(a => a.EndpointName)
            .ToHashSet();

        var uncovered = mappedEndpointNames.Except(coveredEndpointNames).Order().ToArray();
        Assert.True(uncovered.Length == 0, $"These mapped endpoints have no test carrying [CoversEndpoint(\"...\")]: {string.Join(", ", uncovered)}");

        var stale = coveredEndpointNames.Except(mappedEndpointNames).Order().ToArray();
        Assert.True(stale.Length == 0, $"These [CoversEndpoint(\"...\")] names don't match any mapped endpoint (renamed or removed?): {string.Join(", ", stale)}");
    }
}
