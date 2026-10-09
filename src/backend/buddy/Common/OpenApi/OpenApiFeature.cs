using buddy.Features.Users;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace buddy.Common.OpenApi;

// Every OpenAPI document Buddy serves goes through here, so each one gets the same schema, error and
// security rules: the per-feature documents (/openapi/<feature>.json), v1 (/health, /version) and
// the combined "buddy" document that is the published client contract, committed as
// docs/backend/openapi/buddy.json (Meta/OpenApiDocumentTests). See
// docs/backend/analysis/openapi-client-contract.md.
public static class OpenApiFeature
{
    public const string CombinedDocumentName = "buddy";
    public const string SecuritySchemeName = "keycloak";

    public static IServiceCollection AddBuddyOpenApiDocument(this IServiceCollection services, string documentName) =>
        services.AddBuddyOpenApiDocument(documentName, api => api.GroupName == documentName);

    public static IServiceCollection AddBuddyOpenApiDocument(this IServiceCollection services, string documentName, Func<ApiDescription, bool> include) =>
        services.AddOpenApi(documentName, options =>
        {
            // .NET 11 defaults to 3.2, which most client generators can't read yet.
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
            options.ShouldInclude = include;
            options.AddSchemaTransformer<WireSchemaTransformer>();
            options.AddOperationTransformer<ErrorResponsesOperationTransformer>();
            options.AddDocumentTransformer(AddSharedComponentsAsync);
        });

    // The combined document: every operation of every feature, plus /version.
    public static IServiceCollection AddBuddyOpenApi(this IServiceCollection services) =>
        services
            .AddBuddyOpenApiDocument("v1", api => api.GroupName is null)
            .AddBuddyOpenApiDocument(CombinedDocumentName, _ => true);

    // Served in every environment, anonymously and under the default rate limit: the source is
    // public, and each family's instance then describes the exact version it runs.
    public static IEndpointRouteBuilder MapBuddyOpenApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOpenApi().AllowAnonymous();
        return endpoints;
    }

    // The shared error responses, the schema fixes that need the whole document, and one OpenID
    // Connect scheme: the realm's discovery document names the authorize and token endpoints.
    // ValidIssuer is the public realm URL when Authority is a docker-network host.
    private static Task AddSharedComponentsAsync(OpenApiDocument document, Microsoft.AspNetCore.OpenApi.OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var keycloak = context.ApplicationServices.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        var issuer = (keycloak.ValidIssuer ?? keycloak.Authority).TrimEnd('/');

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SecuritySchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OpenIdConnect,
            OpenIdConnectUrl = new Uri($"{issuer}/.well-known/openid-configuration"),
            Description = "Keycloak access token, sent as Authorization: Bearer <token>.",
        };

        SharedResponses.AddTo(document);
        ResponseSchemaRequirements.Apply(document);
        WireSchemaTransformer.RemoveNullFromScalarComponents(document);

        return Task.CompletedTask;
    }
}
