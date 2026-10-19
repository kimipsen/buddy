using Alba;

using buddy.Common;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.UpdateItemDetails;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateItemDetailsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCalendarItemDetails")]
    public async Task A_contributor_can_update_an_items_title_icon_and_color()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Title = "Renamed", Icon = "star", Color = "#123456" }).ToUrl($"/calendars/{calendarId}/items/{item.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<CalendarItemDto>();
        Assert.Equal("Renamed", updated.Title);
        Assert.Equal("star", updated.Icon);
        Assert.Equal("#123456", updated.Color);
    }

    [Fact]
    public async Task A_title_of_200_characters_is_accepted_but_201_is_rejected()
    {
        var (token, url) = await CreateItemAsync();

        await PatchAsync(token, url, new { Title = new string('t', 200), Color = "#123456" }, 200);
        var response = await PatchAsync(token, url, new { Title = new string('t', 201), Color = "#123456" }, 400);

        Assert.Contains("title", response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    [Fact]
    public async Task A_color_of_32_characters_after_trimming_is_accepted_but_33_is_rejected()
    {
        var (token, url) = await CreateItemAsync();

        var accepted = await PatchAsync(token, url, new { Title = "Standup", Color = $" {new string('c', 32)} " }, 200);
        var response = await PatchAsync(token, url, new { Title = "Standup", Color = new string('c', 33) }, 400);

        Assert.Equal(new string('c', 32), accepted.ReadAsJson<CalendarItemDto>().Color);
        Assert.Contains("color", response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    private async Task<(string Token, string Url)> CreateItemAsync()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);
        return (token, $"/calendars/{calendarId}/items/{item.Id}/details");
    }

    private Task<IScenarioResult> PatchAsync(string token, string url, object body, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(body).ToUrl(url);
            _.StatusCodeShouldBe(expectedStatus);
        });
}
