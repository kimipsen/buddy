using System.Diagnostics;

using buddy.Common.Health;
using buddy.Common.Versioning;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace buddy.Common.Observability;

// Traces, metrics and logs through OpenTelemetry -- see docs/backend/observability.md.
//
// Always collected; only exported when OTEL_EXPORTER_OTLP_ENDPOINT is set (any OTLP backend: an
// OpenTelemetry Collector, the Aspire dashboard, Grafana, Azure Monitor...). The other standard
// OTEL_* variables (OTEL_EXPORTER_OTLP_PROTOCOL, OTEL_EXPORTER_OTLP_HEADERS, OTEL_SERVICE_NAME...)
// are read by the SDK itself.
//
// Correlation: UseObservability makes HttpContext.TraceIdentifier -- the requestId an ErrorEnvelope
// returns -- the request's trace id, which every log line (TraceId) and span carries. So a
// user-reported error leads to its logs and its trace.
//
// Secret tokens in URL paths (iCal feeds, shared sleep diaries) never leave the process: the span's
// url.path shows "{token}" instead, and exported logs carry no scopes, because ASP.NET Core's
// RequestPath scope holds the raw path. See gdpr-data-protection.md, Question 8.
public static class ObservabilityFeature
{
    public const string ServiceName = "buddy-api";

    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // A route parameter with this name is a secret (IcalToken, SleepDiaryShareSecret, group shares).
    private const string SecretRouteParameter = "token";

    // ActivitySource and Meter names: Npgsql covers every Marten query, Wolverine every handler.
    private static readonly string[] Sources = ["Npgsql", "Wolverine"];

    public static WebApplicationBuilder AddObservabilityFeature(this WebApplicationBuilder builder)
    {
        // JSON outside Development, so a log shipper can read the scopes (TraceId, RequestId...) as
        // fields; Development keeps the readable single-line console.
        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            });
        }

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                ServiceName,
                serviceVersion: BuildVersion.Current.Version,
                serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Probes poll these every few seconds; tracing them only buries real requests.
                    options.Filter = context => !context.Request.Path.StartsWithSegments(HealthChecksFeature.LivenessPath);
                    // At the end of the request, once routing has named the token parameter.
                    options.EnrichWithHttpResponse = (activity, response) => RedactSecretPath(activity, response.HttpContext);
                })
                .AddHttpClientInstrumentation()
                .AddSource(Sources))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Sources))
            .WithLogging(_ => { }, options =>
            {
                // No scopes: ASP.NET Core's RequestPath scope holds raw paths, tokens included.
                // TraceId and SpanId are fields of every exported log record anyway.
                options.IncludeScopes = false;
                options.IncludeFormattedMessage = true;
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    // First in the pipeline, so every requestId -- error envelopes included -- is the trace id.
    public static IApplicationBuilder UseObservability(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (Activity.Current is { } activity)
            {
                context.TraceIdentifier = activity.TraceId.ToHexString();
            }

            return next(context);
        });

    internal static void RedactSecretPath(Activity activity, HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue(SecretRouteParameter, out var value)
            && value is string token
            && token.Length > 0
            && context.Request.Path.Value is { } path)
        {
            activity.SetTag("url.path", path.Replace(token, "{token}", StringComparison.Ordinal));
        }
    }
}
