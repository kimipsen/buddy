using System.Text.Json.Nodes;

using buddy.Common.Errors;
using buddy.Common.RateLimiting;

using Microsoft.OpenApi;

namespace buddy.Common.OpenApi;

// The responses that are identical on every operation that can return them, defined once under
// components/responses and referenced by ErrorResponsesOperationTransformer.
public static class SharedResponses
{
    public const string Unauthorized = "Unauthorized";
    public const string NotModified = "NotModified";
    public const string TooManyRequests = "TooManyRequests";
    public const string InternalServerError = "InternalServerError";
    public const string ServiceUnavailable = "ServiceUnavailable";

    public static void AddTo(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Responses ??= new Dictionary<string, IOpenApiResponse>();
        var envelope = new OpenApiSchemaReference(nameof(ErrorEnvelope), document);

        document.Components.Responses[Unauthorized] = new OpenApiResponse
        {
            Description = "Unauthorized: the access token is missing, expired or invalid. No body.",
            Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["WWW-Authenticate"] = ErrorResponsesOperationTransformer.StringHeader("The bearer challenge."),
            },
        };
        document.Components.Responses[NotModified] = new OpenApiResponse
        {
            Description = "Not Modified: If-None-Match matched the current ETag. No body.",
            Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["ETag"] = ErrorResponsesOperationTransformer.StringHeader("The same ETag as the cached 200."),
                ["Cache-Control"] = ErrorResponsesOperationTransformer.StringHeader("private, no-cache"),
            },
        };
        document.Components.Responses[TooManyRequests] = Error(
            "Too Many Requests: the caller is over a rate limit.", envelope, RateLimitingFeature.ErrorCode, retryAfter: true);
        document.Components.Responses[InternalServerError] = Error(
            "Internal Server Error: an unexpected failure; requestId matches the server log.", envelope, ExceptionHandlingFeature.InternalErrorCode, retryAfter: false);
        document.Components.Responses[ServiceUnavailable] = Error(
            "Service Unavailable: a dependency (database, identity provider, mail, AI provider) couldn't be reached.", envelope, ExceptionHandlingFeature.DependencyUnavailableCode, retryAfter: true);
    }

    private static OpenApiResponse Error(string description, IOpenApiSchema envelope, string code, bool retryAfter) => new()
    {
        Description = $"{description} ErrorEnvelope code: `{code}`.",
        Content = new Dictionary<string, IOpenApiMediaType>
        {
            ["application/json"] = new OpenApiMediaType { Schema = envelope },
        },
        Headers = retryAfter
            ? new Dictionary<string, IOpenApiHeader>
            {
                ["Retry-After"] = new OpenApiHeader
                {
                    Description = "Seconds to wait before retrying.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
                },
            }
            : null,
        Extensions = new Dictionary<string, IOpenApiExtension>
        {
            [ErrorResponsesOperationTransformer.ErrorCodesExtension] = new JsonNodeExtension(new JsonArray(JsonValue.Create(code))),
        },
    };
}
