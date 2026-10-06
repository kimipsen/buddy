using System.Buffers.Text;
using System.Security.Cryptography;

using Microsoft.Net.Http.Headers;

namespace buddy.Common.Http;

// Conditional GET for endpoints marked .WithETag(): hashes the rendered 200 body into a strong ETag
// and answers a matching If-None-Match with an empty 304. Generic HTTP middleware rather than
// per-handler code, same reasoning as IdempotencyKeyMiddleware -- a body hash can't go stale the way
// a hand-maintained version can, which matters for responses that depend on the clock (the iCal
// feeds' rolling windows, "today" endpoints) as well as on events. See
// docs/backend/analysis/conditional-get-etags.md.
//
// The tag is strong because the hashed bytes are the bytes sent. If response compression is ever
// added (UseResponseCompression, or Caddy's `encode`), it must run outside this middleware so the
// hash is of the uncompressed body, and the tag must become weak (W/"...").
public sealed class ETagMiddleware(RequestDelegate next)
{
    // Store, but revalidate before every reuse: a guardian never sees a list the server hasn't just
    // confirmed, and shared caches never hold a per-user (or token-in-URL) response.
    public const string DefaultCacheControl = "private, no-cache";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method)
            || context.GetEndpoint()?.Metadata.GetMetadata<ETagMetadata>() is null)
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        if (context.Response.StatusCode == StatusCodes.Status200OK)
        {
            var hash = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
            var etag = new EntityTagHeaderValue($"\"{Base64Url.EncodeToString(hash)}\"");

            context.Response.GetTypedHeaders().ETag = etag;

            // An endpoint that set its own value (the iCal feeds, via IcalSubscription) keeps it.
            if (!context.Response.Headers.ContainsKey(HeaderNames.CacheControl))
            {
                context.Response.Headers.CacheControl = DefaultCacheControl;
            }

            if (Matches(context.Request.GetTypedHeaders().IfNoneMatch, etag))
            {
                // RFC 9110: a 304 carries the validator and caching headers but no body.
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = null;
                context.Response.Headers.ContentType = default;
                return;
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    // If-None-Match uses weak comparison (RFC 9110 13.1.2), so W/"x" matches "x".
    private static bool Matches(IList<EntityTagHeaderValue> ifNoneMatch, EntityTagHeaderValue etag) =>
        ifNoneMatch.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(etag, useStrongComparison: false));
}

public sealed class ETagMetadata;

public static class ETagMiddlewareExtensions
{
    public static IApplicationBuilder UseETags(this IApplicationBuilder app) =>
        app.UseMiddleware<ETagMiddleware>();

    // Put on a feature's route group: the middleware ignores it on every non-GET endpoint.
    public static TBuilder WithETag<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new ETagMetadata());
}
