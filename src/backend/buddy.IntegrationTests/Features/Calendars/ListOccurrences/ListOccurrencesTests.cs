using Alba;

using buddy.Common;
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
    public async Task An_all_day_event_occurrence_reports_IsAllDay()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, "Trip", today.AddDays(2), isAllDay: true);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={today:yyyy-MM-dd}&to={today.AddDays(7):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrences = response.ReadAsJson<CalendarItemOccurrenceDto[]>();
        Assert.Contains(occurrences, o => o.Title == "Trip" && o.IsAllDay);
    }

    [Fact]
    public async Task Rejects_a_range_where_to_is_before_from()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var error = await GetInvalidRangeAsync(token, calendarId, today, today.AddDays(-1));

        Assert.Contains("To", error.Details.Keys);
    }

    [Fact]
    public async Task Rejects_a_range_longer_than_the_maximum()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var error = await GetInvalidRangeAsync(token, calendarId, today, today.AddDays(400));

        Assert.Contains("To", error.Details.Keys);
    }

    [Fact]
    public async Task A_range_of_367_days_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var error = await GetInvalidRangeAsync(token, calendarId, today, today.AddDays(367));

        Assert.Contains("To", error.Details.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task A_range_from_zero_up_to_the_maximum_of_366_days_is_accepted(int rangeDays)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={today:yyyy-MM-dd}&to={today.AddDays(rangeDays):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });
    }

    private async Task<ErrorEnvelope> GetInvalidRangeAsync(string token, Guid calendarId, DateOnly from, DateOnly to)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        return error;
    }
}
