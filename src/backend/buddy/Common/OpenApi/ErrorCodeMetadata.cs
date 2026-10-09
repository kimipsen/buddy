namespace buddy.Common.OpenApi;

// The ErrorEnvelope codes an endpoint itself can answer with for one status (409 resend_cooldown,
// 403 email_not_verified, ...). Middleware codes need no declaration: ErrorResponsesOperationTransformer
// adds those by rule. Read only by the OpenAPI document; it changes no behavior.
public sealed record ErrorCodeMetadata(int StatusCode, IReadOnlyList<string> Codes);

public static class ErrorCodeMetadataExtensions
{
    public static TBuilder ProducesErrorCode<TBuilder>(this TBuilder builder, int statusCode, params string[] codes)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new ErrorCodeMetadata(statusCode, codes));
}
