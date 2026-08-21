using Alba;

using buddy.IntegrationTests.Features.Calendars;
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
}
