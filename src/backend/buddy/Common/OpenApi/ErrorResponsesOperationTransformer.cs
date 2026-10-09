using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;

using buddy.Common.Concurrency;
using buddy.Common.Http;
using buddy.Common.Idempotency;
using buddy.Features.Users;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace buddy.Common.OpenApi;

// Documents every error an operation can answer with, and the headers that go with them:
//   - the endpoint's own: a bodiless 403 when its Results<...> includes ForbidHttpResult (which,
//     unlike the other result types, adds no response metadata), and the codes it declares with
//     .ProducesErrorCode(...);
//   - the middleware's, by the same endpoint metadata each middleware checks, so the two can only
//     disagree if one of them changes its condition (Meta/OpenApiDocumentTests checks the result).
// See docs/backend/analysis/openapi-client-contract.md and docs/backend/http-status-codes.md.
public sealed class ErrorResponsesOperationTransformer : IOpenApiOperationTransformer
{
    public const string ErrorCodesExtension = "x-error-codes";
    private const string Json = "application/json";

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var method = context.Description.HttpMethod ?? "";
        var isGet = HttpMethods.IsGet(method);
        var isPost = HttpMethods.IsPost(method);
        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

        var errors = new ErrorResponses(operation, await ErrorEnvelopeSchemaAsync(context, cancellationToken));

        // The endpoint's own responses.
        if (operation.Responses?.ContainsKey("400") == true)
        {
            // BadRequest<ErrorEnvelope> is only ever a validator failure.
            errors.Add(StatusCodes.Status400BadRequest, ValidationProblemExtensions.ErrorCode);
        }

        if (ReturnsForbid(metadata))
        {
            errors.AddWithoutBody(StatusCodes.Status403Forbidden);
        }

        foreach (var declared in metadata.OfType<ErrorCodeMetadata>())
        {
            errors.Add(declared.StatusCode, [.. declared.Codes]);
        }

        // UseAuthorization and ProvisionedUserMiddleware.
        if (requiresAuthorization)
        {
            UseShared(operation, context, StatusCodes.Status401Unauthorized, SharedResponses.Unauthorized);

            if (!metadata.OfType<AllowUnprovisionedUserMetadata>().Any())
            {
                errors.Add(StatusCodes.Status403Forbidden, ProvisionedUserMiddleware.ErrorCode);
            }

            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(OpenApiFeature.SecuritySchemeName, context.Document)] = [] }];
        }

        // RequestBindingFailureMiddleware: an unreadable body or a malformed query value.
        if (operation.RequestBody is not null || operation.Parameters?.Any(p => p.In == ParameterLocation.Query) == true)
        {
            errors.Add(StatusCodes.Status400BadRequest, ValidationProblemExtensions.ErrorCode);
        }

        // IdempotencyKeyMiddleware: only a caller with a Buddy user gets a key.
        if (isPost && requiresAuthorization)
        {
            AddHeaderParameter(operation, IdempotencyKeyMiddleware.HeaderName,
                $"Optional client-generated key (1-{IdempotencyKeyMiddleware.MaxKeyLength} characters). A retry with the same key and the same request replays the first response instead of running again.",
                maxLength: IdempotencyKeyMiddleware.MaxKeyLength);
            errors.Add(StatusCodes.Status400BadRequest, IdempotencyKeyMiddleware.InvalidKeyCode);
            errors.Add(StatusCodes.Status409Conflict,
                IdempotencyKeyMiddleware.KeyReusedCode, IdempotencyKeyMiddleware.KeyInProgressCode, IdempotencyKeyMiddleware.ResponseUnavailableCode);
        }

        // ConcurrencyConflictMiddleware: any command can lose an optimistic-concurrency race.
        if (!isGet)
        {
            errors.Add(StatusCodes.Status409Conflict, ConcurrencyConflictMiddleware.ErrorCode);
        }

        // ETagMiddleware.
        if (isGet && metadata.OfType<ETagMetadata>().Any())
        {
            AddHeaderParameter(operation, "If-None-Match", "An ETag from an earlier 200; when it still matches, the answer is 304 with no body.");
            if (operation.Responses?.TryGetValue("200", out var ok) == true && ok is OpenApiResponse okResponse)
            {
                okResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                okResponse.Headers["ETag"] = StringHeader("A strong validator: a hash of this body.");
                okResponse.Headers["Cache-Control"] = StringHeader("private, no-cache (revalidate with If-None-Match before reuse).");
            }

            UseShared(operation, context, StatusCodes.Status304NotModified, SharedResponses.NotModified);
        }

        // RateLimitingFeature's global limiter and named policies.
        if (!metadata.OfType<DisableRateLimitingAttribute>().Any())
        {
            UseShared(operation, context, StatusCodes.Status429TooManyRequests, SharedResponses.TooManyRequests);
        }

        // ExceptionHandlingFeature.
        UseShared(operation, context, StatusCodes.Status500InternalServerError, SharedResponses.InternalServerError);
        UseShared(operation, context, StatusCodes.Status503ServiceUnavailable, SharedResponses.ServiceUnavailable);

        errors.WriteDescriptions();
    }

    // A response that is the same on every operation is a $ref to components/responses
    // (SharedResponses), which keeps the committed contract readable.
    private static void UseShared(OpenApiOperation operation, OpenApiOperationTransformerContext context, int statusCode, string name)
    {
        operation.Responses ??= [];
        operation.Responses[statusCode.ToString(CultureInfo.InvariantCulture)] = new OpenApiResponseReference(name, context.Document);
    }

    // The handler's declared return type, e.g. Task<Results<Ok<T>, NotFound, ForbidHttpResult>>.
    private static bool ReturnsForbid(IList<object> metadata)
    {
        if (metadata.OfType<MethodInfo>().FirstOrDefault()?.ReturnType is not { } returnType)
        {
            return false;
        }

        if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>) || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            returnType = returnType.GetGenericArguments()[0];
        }

        return returnType == typeof(ForbidHttpResult)
            || (returnType.IsGenericType && returnType.GetGenericArguments().Contains(typeof(ForbidHttpResult)));
    }

    private static async Task<IOpenApiSchema> ErrorEnvelopeSchemaAsync(OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var schema = await context.GetOrCreateSchemaAsync(typeof(ErrorEnvelope), null, cancellationToken);
        context.Document?.AddComponent(nameof(ErrorEnvelope), schema);
        return new OpenApiSchemaReference(nameof(ErrorEnvelope), context.Document);
    }

    private static void AddHeaderParameter(OpenApiOperation operation, string name, string description, int? maxLength = null)
    {
        operation.Parameters ??= [];
        if (operation.Parameters.Any(p => p.In == ParameterLocation.Header && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = false,
            Description = description,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = maxLength is null ? null : 1, MaxLength = maxLength },
        });
    }

    internal static OpenApiHeader StringHeader(string description) =>
        new() { Description = description, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };

    // Collects the codes per status so a status declared by the endpoint and added by a middleware
    // rule (409 resend_cooldown + 409 concurrency_conflict) becomes one response with both.
    private sealed class ErrorResponses(OpenApiOperation operation, IOpenApiSchema errorEnvelope)
    {
        private readonly SortedDictionary<int, SortedSet<string>> _codes = [];

        public void Add(int statusCode, params string[] codes)
        {
            var response = Response(statusCode);
            response.Content ??= new Dictionary<string, IOpenApiMediaType>();
            if (!response.Content.ContainsKey(Json))
            {
                response.Content[Json] = new OpenApiMediaType { Schema = errorEnvelope };
            }

            if (!_codes.TryGetValue(statusCode, out var known))
            {
                _codes[statusCode] = known = [];
            }

            known.UnionWith(codes);
        }

        public void AddWithoutBody(int statusCode) => Response(statusCode);

        public void WriteDescriptions()
        {
            foreach (var (statusCode, codes) in _codes)
            {
                var response = Response(statusCode);
                response.Description = $"{ReasonPhrases.GetReasonPhrase(statusCode)}. ErrorEnvelope code: {string.Join(", ", codes.Select(code => $"`{code}`"))}.";
                response.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                response.Extensions[ErrorCodesExtension] = new JsonNodeExtension(new JsonArray([.. codes.Select(code => (JsonNode)JsonValue.Create(code))]));
            }
        }

        private OpenApiResponse Response(int statusCode)
        {
            operation.Responses ??= [];
            var key = statusCode.ToString(CultureInfo.InvariantCulture);
            if (!operation.Responses.TryGetValue(key, out var existing) || existing is not OpenApiResponse response)
            {
                response = new OpenApiResponse { Description = ReasonPhrases.GetReasonPhrase(statusCode) };
                operation.Responses[key] = response;
            }

            return response;
        }
    }
}
