using Alba;

using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.DeleteItem;

[Collection(BuddyApiCollection.Name)]
public sealed class DeleteItemTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("DeleteCalendarItem")]
    public async Task Deleting_an_item_removes_it_from_the_calendars_item_list()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/calendars/{calendarId}/items/{item.Id}");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBeOk();
        });

        Assert.Empty(response.ReadAsJson<List<CalendarItemDto>>());
    }
}
