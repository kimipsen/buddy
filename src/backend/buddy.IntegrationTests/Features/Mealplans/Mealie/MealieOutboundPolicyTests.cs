using System.Net;

using buddy.Features.Mealplans;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// The SSRF guard -- docs/backend/analysis/mealie-integration.md, Question 3. Pure functions, no host.
public sealed class MealieOutboundPolicyTests
{
    private static readonly MealieOptions Production = new();

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("192.0.0.8")]
    [InlineData("198.18.0.1")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::127.0.0.1")]
    [InlineData("::10.0.0.5")]
    [InlineData("64:ff9b::169.254.169.254")]
    [InlineData("64:ff9b::192.168.1.1")]
    [InlineData("2002:a00:1::1")]
    [InlineData("2002:7f00:1::")]
    public void Private_and_reserved_addresses_are_blocked(string address) =>
        Assert.True(MealieOutboundPolicy.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:93.184.216.34")]
    [InlineData("64:ff9b::93.184.216.34")]
    [InlineData("2002:5db8:d822::1")]
    // Near misses of the NAT64 and 6to4 prefixes: ordinary global addresses whose last 32 bits
    // happen to read as a private IPv4 address, which must not be taken for an embedded one.
    [InlineData("2a00:1450::a00:1")]
    [InlineData("64:ff00::a00:1")]
    [InlineData("2a00:ff9b::a00:1")]
    [InlineData("164:ff9b::a00:1")]
    [InlineData("2a02:a00:1::1")]
    public void Public_addresses_are_allowed(string address) =>
        Assert.False(MealieOutboundPolicy.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("https://mealie.example.com", "https://mealie.example.com")]
    [InlineData("https://mealie.example.com/", "https://mealie.example.com")]
    [InlineData("  https://Mealie.Example.com:8443/  ", "https://mealie.example.com:8443")]
    [InlineData("https://93.184.216.34", "https://93.184.216.34")]
    public void A_public_https_address_is_normalized(string input, string expected)
    {
        var (baseUrl, error) = MealieOutboundPolicy.NormalizeBaseUrl(input, Production);

        Assert.Null(error);
        Assert.Equal(expected, baseUrl);
    }

    [Theory]
    [InlineData("http://mealie.example.com")]
    [InlineData("ftp://mealie.example.com")]
    [InlineData("mealie.example.com")]
    [InlineData("https://user:pass@mealie.example.com")]
    [InlineData("https://mealie.example.com/sub")]
    [InlineData("https://mealie.example.com/?a=1")]
    [InlineData("https://mealie.example.com/#top")]
    [InlineData("https://192.168.1.2")]
    [InlineData("https://[::1]")]
    [InlineData("https://localhost")]
    [InlineData("https://nas.local")]
    [InlineData("https://mealie")]
    public void Anything_else_is_refused_in_production(string input)
    {
        var (baseUrl, error) = MealieOutboundPolicy.NormalizeBaseUrl(input, Production);

        Assert.Null(baseUrl);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void A_self_hosted_installation_can_allow_http_and_private_networks()
    {
        var selfHosted = new MealieOptions { AllowHttp = true, AllowPrivateNetworks = true };

        Assert.Equal("http://mealie:9000", MealieOutboundPolicy.NormalizeBaseUrl("http://mealie:9000", selfHosted).BaseUrl);
        Assert.Equal("http://192.168.1.2:9925", MealieOutboundPolicy.NormalizeBaseUrl("http://192.168.1.2:9925", selfHosted).BaseUrl);
    }

    [Fact]
    public void An_allow_list_limits_which_hosts_may_be_used()
    {
        var restricted = new MealieOptions { AllowedHosts = ["mealie.example.com"] };

        Assert.NotNull(MealieOutboundPolicy.NormalizeBaseUrl("https://MEALIE.example.com", restricted).BaseUrl);
        Assert.Equal(
            "This Buddy installation doesn't allow connecting to that Mealie address.",
            MealieOutboundPolicy.NormalizeBaseUrl("https://other.example.com", restricted).Error);
    }

    [Fact]
    public async Task A_self_hosted_installation_reaches_a_Mealie_on_loopback()
    {
        // A one-shot HTTP server on 127.0.0.1: with private networks allowed, the real client's
        // ConnectCallback must connect to it (production refuses it, see the test below).
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync();
            var buffer = new byte[4096];
            await socket.ReceiveAsync(buffer);
            const string body = "{\"version\":\"v3.29.0\"}";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await socket.SendAsync(System.Text.Encoding.ASCII.GetBytes(response));
        });

        var client = MealieContractTests.CreateClient(allowPrivateNetworks: true);
        var about = await client.GetAboutAsync(new MealieEndpoint($"http://127.0.0.1:{port}", "token"), CancellationToken.None);
        await serve;

        Assert.Equal("v3.29.0", Assert.IsType<MealieResponse<MealieAboutDto>.Ok>(about.Value).Value.Version);
    }

    [Fact]
    public async Task A_refused_connection_is_reported_as_unreachable()
    {
        // A port nothing listens on: the socket fails to connect, and the client says so.
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var client = MealieContractTests.CreateClient(allowPrivateNetworks: true);
        var about = await client.GetAboutAsync(new MealieEndpoint($"http://127.0.0.1:{port}", "token"), CancellationToken.None);

        var failure = about.FailureOf();
        Assert.Equal(MealieFailureKind.Unreachable, failure.Kind);
        Assert.Equal("Buddy couldn't reach your Mealie.", failure.Message);
    }

    [Fact]
    public async Task The_real_client_never_connects_to_a_loopback_address_in_production()
    {
        var client = MealieContractTests.CreateClient(allowPrivateNetworks: false);

        var about = await client.GetAboutAsync(new MealieEndpoint("http://127.0.0.1:9", "token"), CancellationToken.None);

        var failure = about.FailureOf();
        Assert.Equal(MealieFailureKind.Unreachable, failure.Kind);
        Assert.Equal(MealieOutboundPolicy.PrivateAddressMessage, failure.Message);
    }

    [Theory]
    [InlineData("v3.20.0", true)]
    [InlineData("3.20.0", true)]
    [InlineData("v3.29.0", true)]
    [InlineData("v4.0.0", true)]
    [InlineData("v3.20.1-beta", true)]
    [InlineData("nightly", true)]
    [InlineData("develop", true)]
    [InlineData("v3.19.9", false)]
    [InlineData("v2.8.0", false)]
    [InlineData("", false)]
    [InlineData("1", false)]
    public void Only_Mealie_3_20_or_newer_is_supported(string version, bool supported) =>
        Assert.Equal(supported, MealieLiveCheck.IsSupported(version));
}
