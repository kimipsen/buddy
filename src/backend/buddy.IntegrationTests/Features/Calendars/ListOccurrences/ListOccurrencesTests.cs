using Alba;

using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.ListOccurrences;

[Collection(BuddyApiCollection.Name)]
public sealed class ListOccurrencesTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListCalendarOccurrences")]
    public async Task Returns_occurrences_within_the_requested_range()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Standup", today.AddDays(2));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={today:yyyy-MM-dd}&to={today.AddDays(7):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task Rejects_a_range_where_to_is_before_from()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Rejects_a_range_longer_than_the_maximum()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={today:yyyy-MM-dd}&to={today.AddDays(400):yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });
    }
}
