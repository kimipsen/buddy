using Alba;

using buddy.Common;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.UpdateCalendarIcon;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateCalendarIconTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCalendarIcon")]
    public async Task An_owner_can_update_the_calendars_icon()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Icon = "star" }).ToUrl($"/calendars/{calendarId}/icon");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<CalendarResponseDto>();
        Assert.Equal("star", updated.Icon);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_icon_is_rejected(string icon)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Icon = icon }).ToUrl($"/calendars/{calendarId}/icon");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("icon", error.Details.Keys);
    }

    [Theory]
    [InlineData("pictogram:Not A Slug")]
    [InlineData("iiiiiiiiiiiiiiiii")]
    public async Task A_malformed_pictogram_or_an_emoji_longer_than_16_characters_is_rejected(string icon)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var response = await PatchIconAsync(token, calendarId, icon, 400);

        Assert.Contains("icon", response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    [Theory]
    [InlineData("pictogram:brush-teeth-before-bed")]
    [InlineData("iiiiiiiiiiiiiiii")]
    public async Task A_pictogram_or_an_emoji_of_16_characters_is_accepted(string icon)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var response = await PatchIconAsync(token, calendarId, icon, 200);

        Assert.Equal(icon, response.ReadAsJson<CalendarResponseDto>().Icon);
    }

    private Task<IScenarioResult> PatchIconAsync(string token, Guid calendarId, string icon, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Icon = icon }).ToUrl($"/calendars/{calendarId}/icon");
            _.StatusCodeShouldBe(expectedStatus);
        });
}
