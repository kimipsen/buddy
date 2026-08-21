namespace buddy.IntegrationTests.Meta;

// Marks a [Fact]/[Theory] as covering a mapped endpoint, matched by the name passed to
// .WithName(...) on that endpoint in the main project. EndpointCoverageTests reflects over both
// sides and fails if any mapped endpoint has no test carrying a matching attribute -- see
// docs/backend/analysis/integration-testing-strategy.md.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CoversEndpointAttribute(string endpointName) : Attribute
{
    public string EndpointName { get; } = endpointName;
}
