using Alba;

using buddy.Common;
using buddy.Features.Mcp;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mcp;

// Slice 3 of docs/backend/analysis/mcp-server.md: McpGuardianMiddleware. A child's account never
// reaches provisioning or a tool through /mcp, and the ordering around it (member, guardian,
// provisioned) answers each kind of account with its own code.
[Collection(BuddyApiCollection.Name)]
public sealed class McpGuardianTests(BuddyApiFixture fixture)
{
    private const string Initialize =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""";

    private async Task<IScenarioResult> PostMcpAsync(string token, int expectedStatus) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept", "application/json, text/event-stream");
            _.Post.Text(Initialize).ToUrl("/mcp");
            _.ConfigureHttpContext(context => context.Request.ContentType = "application/json");
            _.StatusCodeShouldBe(expectedStatus);
        });

    [Fact]
    public async Task A_child_gets_403_not_a_buddy_guardian_on_initialize()
    {
        var child = await fixture.CreateUserAsync(groups: [KeycloakGroups.Child]);
        var token = await fixture.GetMcpAccessTokenAsync(child);

        var result = await PostMcpAsync(token, 403);

        var error = result.ReadAsJson<ErrorEnvelope>();
        Assert.Equal(McpGuardianMiddleware.ErrorCode, error.Code);
        Assert.False(string.IsNullOrEmpty(error.RequestId));
    }

    [Fact]
    public async Task A_child_is_never_provisioned_through_mcp()
    {
        var child = await fixture.CreateUserAsync(groups: [KeycloakGroups.Child]);
        var mcpToken = await fixture.GetMcpAccessTokenAsync(child);
        await PostMcpAsync(mcpToken, 403);

        // Had the request reached ProvisionedUserMiddleware's provisioning step, the REST pipeline
        // would find a user; the first sign-in logs and creates one only through GET /users/me.
        var restToken = await fixture.GetAccessTokenAsync(child);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {restToken}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task An_account_in_neither_group_gets_not_a_buddy_member_not_not_a_buddy_guardian()
    {
        var stranger = await fixture.CreateUserAsync(groups: []);
        var token = await fixture.GetMcpAccessTokenAsync(stranger);

        var result = await PostMcpAsync(token, 403);

        Assert.Equal(BuddyMemberMiddleware.ErrorCode, result.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task A_guardian_who_has_not_signed_in_to_the_app_gets_user_not_provisioned()
    {
        var guardian = await fixture.CreateUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(guardian);

        var result = await PostMcpAsync(token, 403);

        Assert.Equal(ProvisionedUserMiddleware.ErrorCode, result.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task A_provisioned_guardian_reaches_the_server()
    {
        var (user, _, _) = await fixture.CreateAuthenticatedUserAsync();
        var token = await fixture.GetMcpAccessTokenAsync(user);

        await PostMcpAsync(token, 200);
    }

    [Fact]
    public async Task The_guardian_check_does_not_touch_rest_endpoints_for_a_child()
    {
        var child = await fixture.CreateUserAsync(groups: [KeycloakGroups.Child]);
        var restToken = await fixture.GetAccessTokenAsync(child);

        // A child's own REST token still works on REST: the middleware is scoped to /mcp.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {restToken}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(200);
        });
    }
}
