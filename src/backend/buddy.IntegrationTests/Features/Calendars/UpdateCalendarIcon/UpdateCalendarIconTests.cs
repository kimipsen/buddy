using Alba;

using buddy.IntegrationTests.Features.Calendars;
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
}
