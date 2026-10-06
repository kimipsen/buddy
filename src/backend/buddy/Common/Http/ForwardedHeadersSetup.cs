using Microsoft.AspNetCore.HttpOverrides;

namespace buddy.Common.Http;

// Both deployments put a reverse proxy in front of the API (Caddy on the VM, Envoy ingress on Azure
// Container Apps), so without this RemoteIpAddress is always the proxy -- and the anonymous rate
// limit partition would be one bucket for the whole internet. X-Forwarded-For is honoured only
// from the networks in ForwardedHeaders:KnownNetworks (plus loopback, the framework default):
// trusting it from anywhere would let a client pick a fresh rate-limit bucket per request.
// See docs/backend/analysis/rate-limiting.md.
public static class ForwardedHeadersSetup
{
    public const string KnownNetworksKey = "ForwardedHeaders:KnownNetworks";

    public static IServiceCollection AddForwardedHeadersFromConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var knownNetworks = configuration.GetSection(KnownNetworksKey).Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Only the hop the trusted proxy appended; anything further left is client-supplied.
            options.ForwardLimit = 1;

            foreach (var network in knownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }
}
