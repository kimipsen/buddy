using Alba;

using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.GetCurrentUser;

[Collection(BuddyApiCollection.Name)]
public sealed class GetCurrentUserTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetCurrentUser")]
    public async Task Returns_the_calling_users_profile_created_from_their_keycloak_claims()
    {
        var token = await fixture.GetAccessTokenAsync("alice", "alice-test-pw");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<UserProfileResponse>();

        Assert.Equal("alice", body.UserName);
        Assert.Equal("alice@buddy.test", body.Email.Value);
        Assert.Equal("Alice", body.Name.GivenName);
        Assert.Equal("Anderson", body.Name.FamilyName);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/users/me");
            _.StatusCodeShouldBe(401);
        });
    }

    [Fact]
    public async Task Defaults_a_new_users_language_from_the_accept_language_header()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept-Language", "da-DK,da;q=0.9,en;q=0.8");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<UserProfileResponse>();

        Assert.Equal("da", body.Language);
        Assert.Equal("UTC", body.TimeZoneId);

        // The starting language and time zone ride on UserCreated itself; no follow-up event.
        var events = await fixture.Host.Services.GetRequiredService<IUserEventStore>()
            .ReadAsync(new UserId(body.Id), CancellationToken.None);
        Assert.True(events.First() is UserCreated { Language.Value: "da", TimeZoneId.Value: "UTC" });
        Assert.DoesNotContain(events, e => e is LanguageUpdated or TimeZoneUpdated);
    }

    [Fact]
    public async Task Defaults_a_new_users_language_to_english_when_the_accept_language_header_has_no_supported_match()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader("Accept-Language", "fr-FR,fr;q=0.9");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<UserProfileResponse>();

        Assert.Equal("en", body.Language);
    }

    [Fact]
    public async Task Concurrent_first_requests_all_get_the_same_new_user()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => fixture.Host.Scenario(s =>
        {
            s.WithRequestHeader("Authorization", $"Bearer {token}");
            s.Get.Url("/users/me");
            s.StatusCodeShouldBeOk();
        })));

        Assert.Single(responses.Select(r => r.ReadAsJson<UserProfileResponse>().Id).Distinct());
    }

    [Fact]
    public async Task A_child_sees_the_visual_timer_setting_its_guardian_chose()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await Guardians.GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await Guardians.GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        Assert.True((await GetMeAsync(childToken)).ShowVisualTimers);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { ShowVisualTimers = false }).ToUrl($"/users/me/children/{child.Id}/visual-timers");
            _.StatusCodeShouldBeOk();
        });

        Assert.False((await GetMeAsync(childToken)).ShowVisualTimers);
    }

    [Fact]
    public async Task A_child_sees_the_homework_lengths_its_guardian_chose()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await Guardians.GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await Guardians.GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        Assert.Equal(new HomeworkChunksResponse(15, 5), (await GetMeAsync(childToken)).HomeworkChunks);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Patch.Json(new { FocusMinutes = 10, BreakMinutes = 2 }).ToUrl($"/users/me/children/{child.Id}/homework-chunks");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(new HomeworkChunksResponse(10, 2), (await GetMeAsync(childToken)).HomeworkChunks);
    }

    private async Task<UserProfileResponse> GetMeAsync(string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<UserProfileResponse>();
    }

    private sealed record UserProfileResponse(Guid Id, EmailResponse Email, string UserName, NameResponse Name, string Language, string TimeZoneId, bool ShowVisualTimers = false, HomeworkChunksResponse? HomeworkChunks = null);

    private sealed record HomeworkChunksResponse(int FocusMinutes, int BreakMinutes);

    private sealed record EmailResponse(string Value, bool IsVerified);

    private sealed record NameResponse(string GivenName, string FamilyName);
}
