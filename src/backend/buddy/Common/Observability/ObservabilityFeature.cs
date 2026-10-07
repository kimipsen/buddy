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
// Correlation: every log line carries TraceId/SpanId (the host's default ActivityTrackingOptions)
// and the request's RequestId -- the same value an ErrorEnvelope returns as requestId -- so a
// user-reported error leads to its logs, and from there to the trace.
public static class ObservabilityFeature
{
    public const string ServiceName = "buddy-api";

    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

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
                    // Probes poll these every few seconds; tracing them only buries real requests.
                    options.Filter = context => !context.Request.Path.StartsWithSegments(HealthChecksFeature.LivenessPath))
                .AddHttpClientInstrumentation()
                .AddSource(Sources))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Sources))
            .WithLogging(_ => { }, options =>
            {
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
