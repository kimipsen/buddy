using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.UpdateItemRecurrence;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateItemRecurrenceTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCalendarItemRecurrence")]
    public async Task A_contributor_can_set_a_recurrence_rule_on_an_item()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = new { Frequency = RecurrenceFrequency.Weekly, IntervalCount = 1, Until = (DateOnly?)null } })
                .ToUrl($"/calendars/{calendarId}/items/{item.Id}/recurrence");
            _.StatusCodeShouldBeOk();
        });

        var updated = response.ReadAsJson<CalendarItemDto>();
        Assert.NotNull(updated.Recurrence);
        Assert.Equal(RecurrenceFrequency.Weekly, updated.Recurrence.Frequency);
    }

    [Fact]
    public async Task An_interval_count_below_one_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = new { Frequency = RecurrenceFrequency.Daily, IntervalCount = 0, Until = (DateOnly?)null } })
                .ToUrl($"/calendars/{calendarId}/items/{item.Id}/recurrence");
            _.StatusCodeShouldBe(400);
        });
    }
}
