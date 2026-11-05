using Alba;

using buddy.Common;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.UpdateTimerPreferences;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateTimerPreferencesTests(BuddyApiFixture fixture)
{
    private static readonly object Chosen = new
    {
        Shape = "Bar",
        Size = "Medium",
        Color = "Green",
        ShowMinutes = false,
        Chime = true,
        ChimeSound = "Gong",
        ChimeOnHome = true,
    };

    [Fact]
    [CoversEndpoint("UpdateCurrentTimerPreferences")]
    public async Task A_child_sets_their_own_timer_preferences_and_the_guardian_sees_them()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var response = await PatchAsync(childToken, Chosen, expectedStatus: 200);

        var expected = new TimerPreferencesDto("Bar", "Medium", "Green", ShowMinutes: false, Chime: true, "Gong", ChimeOnHome: true);
        Assert.Equal(expected, response.ReadAsJson<UserWithPreferencesDto>().TimerPreferences);
        Assert.Equal(expected, (await GetMeAsync(childToken)).TimerPreferences);
        Assert.Equal(expected, Assert.Single(await ListChildrenAsync(guardianToken)).TimerPreferences);

        var updated = Assert.Single((await ReadEventsAsync(child.Id)).Select(e => e.Value).OfType<TimerPreferencesUpdated>());
        Assert.Equal(TimerPreferences.Default, updated.Before);
        Assert.Equal(
            new TimerPreferences(TimerShape.Bar, TimerSize.Medium, TimerColor.Green, ShowMinutes: false, Chime: true, ChimeSound.Gong, ChimeOnHome: true),
            updated.After);
    }

    [Fact]
    public async Task A_new_user_has_the_default_preferences_and_the_chime_allowed()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var me = await GetMeAsync(token);

        Assert.Equal(new TimerPreferencesDto("Disc", "Large", "Routine", ShowMinutes: true, Chime: true, "Bell", ChimeOnHome: false), me.TimerPreferences);
        Assert.True(me.AllowChime);
    }

    [Fact]
    public async Task Saving_the_same_preferences_again_appends_no_event()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        await PatchAsync(token, Chosen, expectedStatus: 200);
        var before = (await ReadEventsAsync(userId)).Count;

        var response = await PatchAsync(token, Chosen, expectedStatus: 200);

        Assert.Equal("Gong", response.ReadAsJson<UserWithPreferencesDto>().TimerPreferences.ChimeSound);
        Assert.Equal(before, (await ReadEventsAsync(userId)).Count);
    }

    [Theory]
    [InlineData("shape")]
    [InlineData("size")]
    [InlineData("color")]
    [InlineData("chimeSound")]
    public async Task An_undefined_numeric_enum_value_is_rejected_on_its_field(string field)
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var before = (await ReadEventsAsync(userId)).Count;
        var body = new Dictionary<string, object>
        {
            ["shape"] = "Disc",
            ["size"] = "Large",
            ["color"] = "Routine",
            ["showMinutes"] = true,
            ["chime"] = true,
            ["chimeSound"] = "Bell",
            ["chimeOnHome"] = false,
            [field] = 42,
        };

        var response = await PatchAsync(token, body, expectedStatus: 400);

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal([field], error.Details.Keys);
        Assert.Equal(before, (await ReadEventsAsync(userId)).Count);
    }

    [Fact]
    public async Task An_unknown_enum_name_is_a_bad_request()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await PatchAsync(token, new
        {
            Shape = "Star",
            Size = "Large",
            Color = "Routine",
            ShowMinutes = true,
            Chime = true,
            ChimeSound = "Bell",
            ChimeOnHome = false,
        }, expectedStatus: 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task A_missing_field_is_a_bad_request()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await PatchAsync(token, new { Shape = "Bar" }, expectedStatus: 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
        Assert.Equal("Disc", (await GetMeAsync(token)).TimerPreferences.Shape);
    }

    [Fact]
    public async Task Is_forbidden_when_the_caller_has_no_buddy_user_yet()
    {
        var user = await fixture.CreateUserAsync();
        var token = await fixture.GetAccessTokenAsync(user);

        var response = await PatchAsync(token, Chosen, expectedStatus: 403);

        Assert.Equal(ProvisionedUserMiddleware.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Patch.Json(Chosen).ToUrl("/users/me/timer-preferences");
            _.StatusCodeShouldBe(401);
        });
    }

    private Task<IScenarioResult> PatchAsync(string token, object body, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(body).ToUrl("/users/me/timer-preferences");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private async Task<UserWithPreferencesDto> GetMeAsync(string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<UserWithPreferencesDto>();
    }

    private async Task<ChildSummaryDto[]> ListChildrenAsync(string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<ChildSummaryDto[]>();
    }

    private async Task<IReadOnlyCollection<UserEvent>> ReadEventsAsync(Guid userId) =>
        await fixture.Host.Services.GetRequiredService<IUserEventStore>().ReadAsync(new UserId(userId), CancellationToken.None);

    private sealed record UserWithPreferencesDto(TimerPreferencesDto TimerPreferences, bool AllowChime);
}
