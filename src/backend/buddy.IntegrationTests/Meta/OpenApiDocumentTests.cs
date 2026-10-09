using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Alba;

using buddy.Common.OpenApi;
using buddy.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// The combined OpenAPI document is Buddy's client contract, committed as
// docs/backend/openapi/buddy.json. These tests pin it (so every contract change is a reviewable diff
// of that file) and check the properties a client generator relies on. Regenerate the file with
// `task docs:openapi` (BUDDY_UPDATE_OPENAPI=1). See docs/backend/analysis/openapi-client-contract.md.
[Collection(BuddyApiCollection.Name)]
public sealed class OpenApiDocumentTests(BuddyApiFixture fixture)
{
    private const string UpdateVariable = "BUDDY_UPDATE_OPENAPI";
    private const string KeycloakPlaceholder = "https://keycloak.example/realms/buddy/.well-known/openid-configuration";
    private static readonly string[] Methods = ["get", "put", "post", "patch", "delete"];

    // Every OpenAPI document Buddy registers besides the combined one: "v1" and one per feature.
    private static readonly string[] PartialDocuments =
    [
        "v1", "users", "guardians", "groups", "tasklibrary", "calendars", "medicines", "mealplans",
        "babysitters", "pickups", "worklocations", "printtemplates", "progress", "sleepdiaries",
    ];

    [Fact]
    public async Task The_combined_document_matches_the_committed_contract()
    {
        var actual = Normalize(await FetchAsync(OpenApiFeature.CombinedDocumentName));
        var path = Path.Combine(RepositoryRoot(), "docs", "backend", "openapi", "buddy.json");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"No committed contract at {path}. Run `task docs:openapi` to create it.");
        var expected = await File.ReadAllTextAsync(path);

        if (expected.ReplaceLineEndings() != actual.ReplaceLineEndings())
        {
            var differences = Differences(JsonNode.Parse(expected), JsonNode.Parse(actual), "$").Take(20);
            Assert.Fail(
                "The API contract changed. If that is deliberate, run `task docs:openapi` and commit "
                + $"docs/backend/openapi/buddy.json. First differences:\n{string.Join("\n", differences)}");
        }
    }

    [Fact]
    public async Task No_schema_is_empty()
    {
        var document = await FetchAsync(OpenApiFeature.CombinedDocumentName);

        var empty = document["components"]!["schemas"]!.AsObject()
            .Where(schema => schema.Value is JsonObject { Count: 0 })
            .Select(schema => schema.Key)
            .ToArray();

        Assert.True(empty.Length == 0, $"These schemas are empty ({{}}), so a client can't type them -- usually a custom JSON converter WireSchemaTransformer doesn't know: {string.Join(", ", empty)}");
    }

    [Fact]
    public async Task Every_enum_lists_its_member_names()
    {
        var document = await FetchAsync(OpenApiFeature.CombinedDocumentName);
        var schemas = document["components"]!["schemas"]!.AsObject();
        var enumNames = typeof(OpenApiFeature).Assembly.GetTypes().Where(t => t.IsEnum).Select(t => t.Name).ToHashSet();

        var missing = schemas
            .Where(schema => enumNames.Contains(schema.Key))
            .Where(schema => schema.Value?["enum"] is not JsonArray values || values.Any(v => v?.GetValueKind() != JsonValueKind.String))
            .Select(schema => schema.Key)
            .ToArray();

        Assert.True(missing.Length == 0, $"These enum schemas don't list member names (is JsonStringEnumConverter still registered?): {string.Join(", ", missing)}");
    }

    [Fact]
    public async Task Every_endpoint_that_can_forbid_documents_403()
    {
        var operations = Operations(await FetchAsync(OpenApiFeature.CombinedDocumentName))
            .Where(op => op.Operation["operationId"] is not null)
            .ToDictionary(op => op.Operation["operationId"]!.GetValue<string>(), op => op.Operation);

        var forbidding = fixture.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<MethodInfo>() is { } handler && Mentions(handler.ReturnType, typeof(ForbidHttpResult)))
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .OfType<string>()
            .ToArray();

        Assert.NotEmpty(forbidding);
        var undocumented = forbidding.Where(name => operations.GetValueOrDefault(name)?["responses"]?["403"] is null).Order().ToArray();
        Assert.True(undocumented.Length == 0, $"These endpoints can return ForbidHttpResult but don't document 403: {string.Join(", ", undocumented)}");
    }

    [Fact]
    public async Task Every_error_response_has_the_error_envelope_or_is_bodiless_by_design()
    {
        // 401 is the bearer challenge, 404 hides whether a resource exists, and a 403 from
        // TypedResults.Forbid() carries no body; every other error is an ErrorEnvelope.
        string[] bodiless = ["401", "403", "404"];

        var document = await FetchAsync(OpenApiFeature.CombinedDocumentName);
        var missing = Operations(document)
            .SelectMany(op => op.Operation["responses"]!.AsObject().Select(response => (op.Name, Status: response.Key, Response: Resolve(document, response.Value))))
            .Where(r => r.Status[0] is '4' or '5' && !bodiless.Contains(r.Status))
            .Where(r => r.Response?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>() != "#/components/schemas/ErrorEnvelope")
            .Select(r => $"{r.Name} {r.Status}")
            .ToArray();

        Assert.True(missing.Length == 0, $"These error responses aren't documented as an ErrorEnvelope: {string.Join(", ", missing)}");
    }

    [Fact]
    public async Task The_partial_documents_together_hold_exactly_the_combined_operations()
    {
        var combined = Operations(await FetchAsync(OpenApiFeature.CombinedDocumentName)).Select(op => op.Name).ToArray();
        var partial = new List<string>();
        foreach (var name in PartialDocuments)
        {
            partial.AddRange(Operations(await FetchAsync(name)).Select(op => op.Name));
        }

        Assert.Equal(combined.Order(), partial.Order());
    }

    private async Task<JsonObject> FetchAsync(string documentName)
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/openapi/{documentName}.json");
            s.StatusCodeShouldBeOk();
        });

        return JsonNode.Parse(await result.ReadAsTextAsync())!.AsObject();
    }

    private static IEnumerable<(string Name, JsonObject Operation)> Operations(JsonObject document) =>
        document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject()
            .Where(op => Methods.Contains(op.Key))
            .Select(op => ($"{op.Key.ToUpperInvariant()} {path.Key}", op.Value!.AsObject())));

    // A response that is a $ref to components/responses (SharedResponses).
    private static JsonNode? Resolve(JsonObject document, JsonNode? response) =>
        response?["$ref"]?.GetValue<string>() is { } reference
            ? document["components"]!["responses"]![reference.Split('/')[^1]]
            : response;

    private static bool Mentions(Type type, Type wanted) =>
        type == wanted || (type.IsGenericType && type.GetGenericArguments().Any(argument => Mentions(argument, wanted)));

    // The two values that depend on the host: the server URL and the Keycloak realm.
    private static string Normalize(JsonObject document)
    {
        document.Remove("servers");
        if (document["components"]?["securitySchemes"]?[OpenApiFeature.SecuritySchemeName] is JsonObject scheme)
        {
            scheme["openIdConnectUrl"] = KeycloakPlaceholder;
        }

        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static IEnumerable<string> Differences(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            foreach (var key in expectedObject.Select(p => p.Key).Union(actualObject.Select(p => p.Key)))
            {
                foreach (var difference in Differences(expectedObject[key], actualObject[key], $"{path}.{key}"))
                {
                    yield return difference;
                }
            }
        }
        else if (!JsonNode.DeepEquals(expected, actual))
        {
            yield return $"{path}: expected {Short(expected)}, got {Short(actual)}";
        }
    }

    private static string Short(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "(missing)";
        return text.Length <= 120 ? text : text[..117] + "...";
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "backend")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No repository root (a folder with docs/backend) above {AppContext.BaseDirectory}.");
    }
}
