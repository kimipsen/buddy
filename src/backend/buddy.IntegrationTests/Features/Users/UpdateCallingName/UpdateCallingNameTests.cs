using Alba;

using buddy.Common;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.UpdateCallingName;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateCallingNameTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCurrentCallingName")]
    public async Task Sets_the_calling_users_calling_name_trimmed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var body = await PatchAsync(token, new { CallingName = "  Mor  " }, expectedStatus: 200);

        Assert.Equal("Mor", body!.CallingName);
        Assert.Equal("Mor", (await GetCurrentUserAsync(token)).CallingName);
    }

    [Fact]
    public async Task A_new_user_has_no_calling_name()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        Assert.Equal("", (await GetCurrentUserAsync(token)).CallingName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task An_empty_value_clears_the_calling_name(string? cleared)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await PatchAsync(token, new { CallingName = "Far" }, expectedStatus: 200);

        var body = await PatchAsync(token, new { CallingName = cleared }, expectedStatus: 200);

        Assert.Equal("", body!.CallingName);
    }

    [Fact]
    public async Task Setting_the_same_calling_name_again_appends_no_event()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        await PatchAsync(token, new { CallingName = "Mor" }, expectedStatus: 200);

        await PatchAsync(token, new { CallingName = "Mor" }, expectedStatus: 200);

        var events = await fixture.Host.Services.GetRequiredService<IUserEventStore>().ReadAsync(new UserId(userId), CancellationToken.None);
        Assert.Single(events, e => e.EventType == nameof(CallingNameUpdated));
    }

    [Fact]
    public async Task Accepts_exactly_the_maximum_length()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var longest = new string('a', UpdateCallingNameValidator.MaxLength);

        var body = await PatchAsync(token, new { CallingName = longest }, expectedStatus: 200);

        Assert.Equal(longest, body!.CallingName);
    }

    [Fact]
    public async Task Rejects_a_calling_name_over_the_maximum_length()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { CallingName = new string('a', UpdateCallingNameValidator.MaxLength + 1) }).ToUrl("/users/me/calling-name");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("CallingName", error.Details.Keys);
    }

    [Fact]
    public async Task Is_forbidden_when_the_caller_has_no_buddy_user_yet()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { CallingName = "Mor" }).ToUrl("/users/me/calling-name");
            _.StatusCodeShouldBe(403);
        });

        Assert.Equal(ProvisionedUserMiddleware.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    private async Task<CallingNameResponse?> PatchAsync(string token, object body, int expectedStatus)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(body).ToUrl("/users/me/calling-name");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<CallingNameResponse>() : null;
    }

    private async Task<CallingNameResponse> GetCurrentUserAsync(string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<CallingNameResponse>();
    }

    private sealed record CallingNameResponse(string CallingName);
}
