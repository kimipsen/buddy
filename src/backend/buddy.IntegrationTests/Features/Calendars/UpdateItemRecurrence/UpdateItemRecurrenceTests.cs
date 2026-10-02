using Alba;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

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

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = new { Frequency = RecurrenceFrequency.Daily, IntervalCount = 0, Until = (DateOnly?)null } })
                .ToUrl($"/calendars/{calendarId}/items/{item.Id}/recurrence");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Recurrence.IntervalCount", error.Details.Keys);
    }

    [Fact]
    public async Task A_recurrence_ending_before_the_items_start_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, date: start);
        Assert.NotNull(item);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = new { Frequency = RecurrenceFrequency.Daily, IntervalCount = 1, Until = (DateOnly?)start.AddDays(-1) } })
                .ToUrl($"/calendars/{calendarId}/items/{item.Id}/recurrence");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Recurrence.Until", error.Details.Keys);
    }

    [Fact]
    public async Task Clearing_the_recurrence_makes_the_item_a_one_off_again()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, date: start);
        Assert.NotNull(item);
        var url = $"/calendars/{calendarId}/items/{item.Id}/recurrence";

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = new { Frequency = RecurrenceFrequency.Daily, IntervalCount = 1, Until = (DateOnly?)start.AddDays(3) } }).ToUrl(url);
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Recurrence = (object?)null }).ToUrl(url);
            _.StatusCodeShouldBeOk();
        });

        Assert.Null(response.ReadAsJson<CalendarItemDto>().Recurrence);

        var occurrences = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={start:yyyy-MM-dd}&to={start.AddDays(3):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        Assert.Single(occurrences.ReadAsJson<List<CalendarItemOccurrenceDto>>());
    }

    [Fact]
    public async Task Re_sending_the_same_recurrence_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, date: start);
        Assert.NotNull(item);
        var body = new { Recurrence = new { Frequency = RecurrenceFrequency.Weekly, IntervalCount = 2, Until = (DateOnly?)start.AddDays(30) } };
        var items = fixture.Host.Services.GetRequiredService<ICalendarItemEventStore>();
        var id = new CalendarItemId(item.Id);

        await PatchAsync();
        var afterFirst = (await items.ReadAsync(id, CancellationToken.None)).Count;
        await PatchAsync();

        Assert.Equal(afterFirst, (await items.ReadAsync(id, CancellationToken.None)).Count);

        Task PatchAsync() => fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(body).ToUrl($"/calendars/{calendarId}/items/{item.Id}/recurrence");
            _.StatusCodeShouldBeOk();
        });
    }
}
