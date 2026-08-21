using Alba;

using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.ListItems;

[Collection(BuddyApiCollection.Name)]
public sealed class ListItemsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListCalendarItems")]
    public async Task Lists_the_calendars_items_in_schedule_order()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Later", today.AddDays(5));
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Sooner", today.AddDays(1));

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBeOk();
        });

        var items = response.ReadAsJson<List<CalendarItemDto>>();

        Assert.Equal(2, items.Count);
        Assert.Equal("Sooner", items[0].Title);
        Assert.Equal("Later", items[1].Title);
    }

    [Fact]
    public async Task A_non_member_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Private");
        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {outsiderToken}");
            _.Get.Url($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBe(404);
        });
    }
}
