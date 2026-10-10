namespace buddy.Common.Http;

// The API only serves JSON (and .ics feeds), never a page, so nothing may load or frame it. Set by
// the API itself rather than the edge proxy, because Azure Container Apps has none that can add
// headers. The app's own headers live in src/frontend/buddy/Caddyfile; Keycloak sends its own
// (realm Security Defenses).
//
// Added in OnStarting so every response carries them, including the exception handler's 500 (which
// clears the headers set before it ran), 429s and 304s.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // No includeSubDomains: the API's domain may share a parent with unrelated hosts.
    public const string StrictTransportSecurity = "max-age=31536000";
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = ContentSecurityPolicy;

            // Browsers ignore it over plain http (RFC 6797), so it is sent regardless of the scheme
            // the proxy forwarded. Never for a loopback host: HSTS is per host, not per port, so
            // pinning localhost would break every other http dev server on the machine.
            if (!IsLoopback(context.Request.Host.Host))
            {
                headers.StrictTransportSecurity = StrictTransportSecurity;
            }

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host is "127.0.0.1" or "[::1]" or "::1";
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
