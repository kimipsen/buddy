using System.Net;
using System.Net.Sockets;

namespace buddy.Features.Mealplans;

// The first outbound call in Buddy to an address a user typed in -- the guard against using Buddy's
// server to probe its own network (SSRF). See docs/backend/analysis/mealie-integration.md, Question 3.
public static class MealieOutboundPolicy
{
    public const int MaxBaseUrlLength = 300;

    // Null with a normalized base URL ("https://mealie.example.com", or with a port), or an error
    // message. Checked when a URL is saved; the address itself is checked again on every connection
    // (ConnectAsync), because a name can resolve somewhere else later (DNS rebinding).
    public static (string? BaseUrl, string? Error) NormalizeBaseUrl(string input, MealieOptions options)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return (null, "Enter the full address of your Mealie, starting with https://.");
        }

        if (uri.Scheme == "http" && !options.AllowHttp)
        {
            return (null, "Buddy only connects to Mealie over https.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath.TrimEnd('/').Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return (null, "Enter only the address of your Mealie, without a path, such as https://mealie.example.com.");
        }

        if (options.AllowedHosts.Length > 0 && !options.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "This Buddy installation doesn't allow connecting to that Mealie address.");
        }

        if (!options.AllowPrivateNetworks && IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) && IsBlocked(literal))
        {
            return (null, PrivateAddressMessage);
        }

        if (!options.AllowPrivateNetworks && (uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || !uri.Host.Contains('.', StringComparison.Ordinal)))
        {
            return (null, PrivateAddressMessage);
        }

        return (uri.GetLeftPart(UriPartial.Authority), null);
    }

    public const string PrivateAddressMessage =
        "Buddy can't reach a Mealie on a home or private network. Give it a public https address, for example with a Cloudflare Tunnel or Tailscale Funnel.";

    // Loopback, private (RFC 1918, fc00::/7), link-local (169.254/16, fe80::/10), CGNAT (100.64/10),
    // multicast, unspecified, and the IPv4 "this network" and reserved ranges.
    public static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();

            return b[0] switch
            {
                0 or 10 or 127 => true,
                100 => b[1] is >= 64 and <= 127,
                169 => b[1] == 254,
                172 => b[1] is >= 16 and <= 31,
                192 => (b[1] == 168) || (b[1] == 0 && b[2] is 0 or 2),
                198 => b[1] is 18 or 19,
                >= 224 => true,
                _ => false,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();

            // IPv6 forms that carry an IPv4 address are judged by that address: IPv4-compatible
            // (::a.b.c.d), NAT64 (64:ff9b::/96) and 6to4 (2002:aabb:ccdd::/16).
            if (EmbeddedIPv4(b) is { } embedded)
            {
                return IsBlocked(embedded);
            }

            return address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || (b[0] & 0xfe) == 0xfc;
        }

        // Stryker disable once Boolean : an IPAddress is always InterNetwork or InterNetworkV6; this fail-closed default is never reached
        return true;
    }

    private static IPAddress? EmbeddedIPv4(byte[] b)
    {
        static bool Zero(byte[] bytes, int from, int to) => bytes[from..to].All(x => x == 0);

        // ::a.b.c.d (:: and ::1 never get here: IsBlocked handles them first).
        if (Zero(b, 0, 12))
        {
            return new IPAddress(b[12..16]);
        }

        // 64:ff9b::a.b.c.d
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b && Zero(b, 4, 12))
        {
            return new IPAddress(b[12..16]);
        }

        // 2002:aabb:ccdd::
        return b[0] == 0x20 && b[1] == 0x02 ? new IPAddress(b[2..6]) : null;
    }

    // SocketsHttpHandler.ConnectCallback: resolve the host here, drop every blocked address, and
    // connect to one we checked -- never to a name the handler would resolve again by itself.
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, bool allowPrivateNetworks, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
        var allowed = allowPrivateNetworks ? addresses : [.. addresses.Where(a => !IsBlocked(a))];

        if (allowed.Length == 0)
        {
            throw new MealieAddressBlockedException(host);
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

public sealed class MealieAddressBlockedException(string host)
    : IOException($"The Mealie host '{host}' resolves only to private or reserved addresses.");
