using System.Net;

using Alba;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Common.RateLimiting;

// Runs against its own API host with tiny limits (the shared host's are raised out of reach, see
// BuddyApiFixture). Buckets never refill during a test (one-hour periods), and every test uses a
// fresh user, feed id or client IP so the tests can't drain each other's partitions.
// See docs/backend/analysis/rate-limiting.md.
[Collection(BuddyApiCollection.Name)]
public sealed class RateLimitingTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private const int UserBurst = 3;
    private const int IpBurst = 3;
    private const int FeedBurst = 2;
    private const string AllowedOrigin = "https://app.rate-limit.test";
    private const string TrustedProxyNetwork = "10.0.0.0/8";

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await fixture.CreateHostAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Authenticated:TokenLimit"] = $"{UserBurst}",
            ["RateLimiting:Authenticated:TokensPerPeriod"] = "1",
            ["RateLimiting:Authenticated:ReplenishmentPeriod"] = "01:00:00",
            ["RateLimiting:Anonymous:TokenLimit"] = $"{IpBurst}",
            ["RateLimiting:Anonymous:TokensPerPeriod"] = "1",
            ["RateLimiting:Anonymous:ReplenishmentPeriod"] = "01:00:00",
            ["RateLimiting:IcalFeed:TokenLimit"] = $"{FeedBurst}",
            ["RateLimiting:IcalFeed:TokensPerPeriod"] = "1",
            ["RateLimiting:IcalFeed:ReplenishmentPeriod"] = "01:00:00",
            ["RateLimiting:AiAssistant:PermitLimit"] = "1",
            ["RateLimiting:AiAssistant:Window"] = "01:00:00",
            ["RateLimiting:OutboundEmail:PermitLimit"] = "1",
            ["RateLimiting:OutboundEmail:Window"] = "01:00:00",
            ["Cors:AllowedOrigins:0"] = AllowedOrigin,
            ["ForwardedHeaders:KnownNetworks:0"] = TrustedProxyNetwork
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task A_user_over_their_burst_gets_429_with_retry_after_and_the_rate_limited_envelope()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        for (var i = 0; i < UserBurst; i++)
        {
            Assert.Equal(200, await StatusAsync(Get("/users/me", token: token)));
        }

        var rejected = await _host.Scenario(_ =>
        {
            Get("/users/me", token: token)(_);
            _.StatusCodeShouldBe(429);
        });

        var retryAfter = rejected.Context.Response.Headers.RetryAfter.ToString();
        Assert.True(int.TryParse(retryAfter, out var seconds) && seconds > 0, $"Retry-After was '{retryAfter}'");

        var envelope = rejected.ReadAsJson<ErrorEnvelope>();
        Assert.Equal(RateLimitingFeature.ErrorCode, envelope.Code);
        Assert.False(string.IsNullOrEmpty(envelope.RequestId));
    }

    [Fact]
    public async Task One_users_exhausted_bucket_does_not_throttle_another_user()
    {
        var (_, first, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, second, _) = await fixture.CreateAuthenticatedUserAsync();

        // Same client IP for both: authenticated callers are partitioned by user, not address.
        var sharedIp = NewClientIp();
        await DrainAsync(Get("/users/me", sharedIp, first), UserBurst);

        Assert.Equal(429, await StatusAsync(Get("/users/me", sharedIp, first)));
        Assert.Equal(200, await StatusAsync(Get("/users/me", sharedIp, second)));
    }

    [Fact]
    public async Task An_anonymous_ip_over_its_burst_gets_429_and_another_ip_does_not()
    {
        var ip = NewClientIp();
        await DrainAsync(Get("/version", ip), IpBurst);

        Assert.Equal(429, await StatusAsync(Get("/version", ip)));
        Assert.Equal(200, await StatusAsync(Get("/version", NewClientIp())));
    }

    [Fact]
    public async Task Ipv6_clients_in_the_same_64_share_one_bucket()
    {
        // Rotating the interface identifier (the low 64 bits) on every request doesn't escape the bucket.
        var network = NewClientIp().GetAddressBytes();
        IPAddress InSameNetwork()
        {
            var bytes = (byte[])network.Clone();
            Random.Shared.NextBytes(bytes.AsSpan(8));
            return new IPAddress(bytes);
        }

        await DrainAsync(Get("/version", InSameNetwork()), IpBurst);

        Assert.Equal(429, await StatusAsync(Get("/version", InSameNetwork())));
    }

    [Fact]
    public async Task An_invalid_bearer_token_is_counted_against_the_client_ip()
    {
        var ip = NewClientIp();
        await DrainAsync(Get("/version", ip, token: "not-a-real-token"), IpBurst);

        Assert.Equal(429, await StatusAsync(Get("/version", ip)));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Health_probes_are_never_rate_limited(string path)
    {
        var ip = NewClientIp();

        for (var i = 0; i < IpBurst * 3; i++)
        {
            Assert.Equal(200, await StatusAsync(Get(path, ip)));
        }
    }

    [Fact]
    public async Task A_throttled_post_does_not_reserve_its_idempotency_key()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var key = Guid.NewGuid().ToString();
        await DrainAsync(Get("/users/me", token: token), UserBurst);

        await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Idempotency-Key", key);
            _.Post.Json(new { Name = "Throttled" }).ToUrl("/groups/");
            _.StatusCodeShouldBe(429);
        });

        // Same database, unthrottled host: had the 429 reserved the key, this would replay or conflict.
        var created = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Idempotency-Key", key);
            _.Post.Json(new { Name = "Throttled" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });
        Assert.Contains("Throttled", created.ReadAsText());
    }

    [Fact]
    public async Task A_429_carries_cors_headers_so_the_frontend_can_read_it()
    {
        var ip = NewClientIp();
        await DrainAsync(Get("/version", ip), IpBurst);

        var rejected = await _host.Scenario(_ =>
        {
            Get("/version", ip)(_);
            _.WithRequestHeader("Origin", AllowedOrigin);
            _.StatusCodeShouldBe(429);
        });

        Assert.Equal(AllowedOrigin, rejected.Context.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    [Theory]
    [InlineData("calendars")]
    [InlineData("mealplans")]
    public async Task A_feed_link_over_its_burst_gets_429_but_another_token_for_the_same_feed_does_not(string feedRoute)
    {
        var feedId = Guid.NewGuid();
        var hammered = $"/{feedRoute}/{feedId}/ical/{Guid.NewGuid():N}";

        // A fresh IP per request, so only the per-feed policy can be what throttles.
        for (var i = 0; i < FeedBurst; i++)
        {
            Assert.Equal(404, await StatusAsync(Get(hammered, NewClientIp())));
        }

        Assert.Equal(429, await StatusAsync(Get(hammered, NewClientIp())));

        // Keyed by token as well as feed id: wrong-token requests can't drain the real link's bucket.
        Assert.Equal(404, await StatusAsync(Get($"/{feedRoute}/{feedId}/ical/{Guid.NewGuid():N}", NewClientIp())));
    }

    [Fact]
    public async Task A_valid_feed_link_is_served_until_its_own_burst_runs_out()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Rate limited");
        var issued = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);
        var feed = $"/calendars/{calendarId}/ical/{issued.Token}";

        for (var i = 0; i < FeedBurst; i++)
        {
            Assert.Equal(200, await StatusAsync(Get(feed, NewClientIp())));
        }

        Assert.Equal(429, await StatusAsync(Get(feed, NewClientIp())));
    }

    [Theory]
    [InlineData("/mealplans/children/{0}/ai/sessions")]
    [InlineData("/groups/{0}/invites")]
    public async Task A_costly_endpoint_policy_throttles_before_the_users_global_bucket_runs_out(string urlFormat)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var url = string.Format(System.Globalization.CultureInfo.InvariantCulture, urlFormat, Guid.NewGuid());

        // The first request isn't throttled (whatever the handler makes of an unknown id); the
        // second is, after only one of the user's UserBurst global tokens was spent -- so the named
        // policy is what rejected it. (Not followed by a global-bucket check: on a rejection
        // RateLimitingMiddleware retries the acquire, so a rejected request costs two global tokens.)
        Assert.NotEqual(429, await StatusAsync(Post(url, token)));
        Assert.Equal(429, await StatusAsync(Post(url, token)));
    }

    [Fact]
    public async Task X_forwarded_for_from_a_trusted_proxy_picks_the_partition()
    {
        var proxy = IPAddress.Parse("10.1.2.3");
        var client = NewClientIp();
        await DrainAsync(Get("/version", proxy, forwardedFor: client), IpBurst);

        Assert.Equal(429, await StatusAsync(Get("/version", proxy, forwardedFor: client)));
        Assert.Equal(200, await StatusAsync(Get("/version", proxy, forwardedFor: NewClientIp())));
    }

    [Fact]
    public async Task X_forwarded_for_from_an_untrusted_address_is_ignored()
    {
        var untrusted = NewClientIp();

        // A new spoofed client address on every request still lands in the sender's own bucket.
        await DrainAsync(Get("/version", untrusted, forwardedFor: NewClientIp()), IpBurst);

        Assert.Equal(429, await StatusAsync(Get("/version", untrusted, forwardedFor: NewClientIp())));
    }

    [Fact]
    public async Task A_non_positive_limit_fails_the_host_at_startup()
    {
        var startup = fixture.CreateHostAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:IcalFeed:TokenLimit"] = "0"
        });

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => startup);
        Assert.Contains("RateLimiting", exception.ToString());
    }

    private static Action<Scenario> Get(string url, IPAddress? ip = null, string? token = null, IPAddress? forwardedFor = null) => _ =>
    {
        Configure(_, ip, token, forwardedFor);
        _.Get.Url(url);
    };

    private static Action<Scenario> Post(string url, string token) => _ =>
    {
        Configure(_, null, token, null);
        _.Post.Json(new { }).ToUrl(url);
    };

    private static void Configure(Scenario scenario, IPAddress? ip, string? token, IPAddress? forwardedFor)
    {
        if (token is not null)
        {
            scenario.WithRequestHeader("Authorization", $"Bearer {token}");
        }

        if (forwardedFor is not null)
        {
            scenario.WithRequestHeader("X-Forwarded-For", forwardedFor.ToString());
        }

        var remote = ip ?? NewClientIp();
        scenario.ConfigureHttpContext(context => context.Connection.RemoteIpAddress = remote);
    }

    private async Task<int> StatusAsync(Action<Scenario> request)
    {
        var result = await _host.Scenario(_ =>
        {
            request(_);
            _.IgnoreStatusCode();
        });

        return result.Context.Response.StatusCode;
    }

    private async Task DrainAsync(Action<Scenario> request, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.NotEqual(429, await StatusAsync(request));
        }
    }

    // A unique address in the IPv6 documentation range (2001:db8::/32) per call.
    private static IPAddress NewClientIp()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        bytes[0] = 0x20;
        bytes[1] = 0x01;
        bytes[2] = 0x0d;
        bytes[3] = 0xb8;
        return new IPAddress(bytes);
    }
}
