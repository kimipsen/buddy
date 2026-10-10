using Alba;

using buddy.Common.Http;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Common.Http;

[Collection(BuddyApiCollection.Name)]
public sealed class SecurityHeadersMiddlewareTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_response_carries_the_security_headers()
    {
        var response = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/health");
            s.WithRequestHeader("Host", "api.example.org");
            s.StatusCodeShouldBeOk();
        });

        var headers = response.Context.Response.Headers;
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, headers.ContentSecurityPolicy.ToString());
        Assert.Equal(SecurityHeadersMiddleware.StrictTransportSecurity, headers.StrictTransportSecurity.ToString());
    }

    [Fact]
    public async Task An_error_response_carries_them_too()
    {
        var response = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/users/me");
            s.StatusCodeShouldBe(401);
        });

        Assert.Equal("nosniff", response.Context.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, response.Context.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task A_loopback_host_gets_no_strict_transport_security()
    {
        var response = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/health");
            s.WithRequestHeader("Host", "localhost");
            s.StatusCodeShouldBeOk();
        });

        Assert.False(response.Context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }
}
