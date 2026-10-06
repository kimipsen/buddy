using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using buddy.Common.Configuration;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace buddy.Common.RateLimiting;

// Request-rate limits for every endpoint -- see docs/backend/analysis/rate-limiting.md. A global
// limiter partitioned by caller (Keycloak subject, else client IP) covers everything; named
// policies stack a tighter limit on the endpoints that cost the most. Counters live in memory per
// replica on purpose: this protects Postgres from floods, it isn't a quota. Different from
// ResendCooldown, which is a per-target state rule decided in the handler (409, not 429).
public static class RateLimitingFeature
{
    public const string ErrorCode = "rate_limited";

    public const string IcalFeedPolicy = "ical-feed";
    public const string AiAssistantPolicy = "ai-assistant";
    public const string OutboundEmailPolicy = "outbound-email";

    public static IServiceCollection AddRateLimitingFeature(this IServiceCollection services)
    {
        services.AddValidatedOptions<RateLimitingOptions>(RateLimitingOptions.SectionName)
            .Validate(options => options.IsValid(), "Every RateLimiting limit, token count and period must be greater than zero.");

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((options, configured) =>
            {
                var limits = configured.Value;

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = RejectAsync;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    // A missing, expired or invalid token never authenticates, so it lands in the IP
                    // partition and can't mint fresh per-user buckets.
                    IsAuthenticated(context)
                        ? RateLimitPartition.GetTokenBucketLimiter(UserKey(context), _ => limits.Authenticated.ToOptions())
                        : RateLimitPartition.GetTokenBucketLimiter(IpKey(context), _ => limits.Anonymous.ToOptions()));

                // Not by IP: Google and Outlook poll every subscribed feed from shared server pools.
                // By feed id AND token hash: the id isn't secret, so keying on it alone would let
                // anyone drain a family's bucket with wrong tokens. Hashed so raw tokens never sit in
                // limiter memory.
                options.AddPolicy(IcalFeedPolicy, context =>
                    RateLimitPartition.GetTokenBucketLimiter(IcalFeedKey(context), _ => limits.IcalFeed.ToOptions()));

                options.AddPolicy(AiAssistantPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter($"ai:{CallerKey(context)}", _ => limits.AiAssistant.ToOptions()));

                options.AddPolicy(OutboundEmailPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter($"email:{CallerKey(context)}", _ => limits.OutboundEmail.ToOptions()));
            });

        return services;
    }

    private static bool IsAuthenticated(HttpContext context) => context.User.Identity?.IsAuthenticated == true;

    private static string UserKey(HttpContext context) => $"user:{context.User.GetKeycloakSubject().Value}";

    // Real client address once UseForwardedHeaders has run (see ForwardedHeadersSetup).
    private static string IpKey(HttpContext context) => $"ip:{ClientNetwork(context.Connection.RemoteIpAddress)}";

    // IPv6 by /64: one home or mobile connection is normally handed a whole /64, so keying by the
    // full address would let a single client rotate through fresh buckets.
    private static string ClientNetwork(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

    // The per-user policies sit on endpoints that require authorization, but the limiter runs
    // before authorization: an anonymous request is partitioned by IP and gets its 401 afterwards.
    private static string CallerKey(HttpContext context) => IsAuthenticated(context) ? UserKey(context) : IpKey(context);

    private static string IcalFeedKey(HttpContext context)
    {
        var feed = context.GetRouteValue("calendarId") is { } calendarId
            ? $"calendar:{calendarId}"
            : $"mealplan:{context.GetRouteValue("mealPlanId")}";
        var token = context.GetRouteValue("token") as string ?? string.Empty;

        return $"ical:{feed}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))}";
    }

    private static async ValueTask RejectAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;

        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // Partition kind and route only -- never the subject, address or a feed token.
        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RateLimitingFeature))
            .LogInformation(
                "Rate limit exceeded for {CallerKind} caller on {Endpoint}",
                IsAuthenticated(context) ? "authenticated" : "anonymous",
                context.GetEndpoint()?.DisplayName ?? $"{context.Request.Method} (unmatched route)");

        var envelope = new ErrorEnvelope(
            ErrorCode,
            "Too many requests. Try again later.",
            new Dictionary<string, string[]>(),
            context.TraceIdentifier);

        var json = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        await context.Response.WriteAsJsonAsync(envelope, json, cancellationToken);
    }
}
