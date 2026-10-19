using Alba;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars;

// A calendar item's icon may be a pictogram ("pictogram:<name>") instead of an emoji
// (docs/frontend/analysis/pictograms.md, Question 2). The backend checks the shape only.
[Collection(BuddyApiCollection.Name)]
public sealed class PictogramIconTests(BuddyApiFixture fixture)
{
    public static TheoryData<string> MalformedPictograms =>
    [
        "pictogram:",
        "pictogram:Brush-teeth",
        "pictogram:brush teeth",
        "pictogram:brush--teeth",
        "pictogram:-brush",
        "pictogram:../x",
        "pictogram:brush-teeth\n",
        "pictogram:" + new string('a', 60),
    ];

    [Fact]
    public async Task A_task_can_be_created_and_updated_with_a_pictogram_icon()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var created = await CreateTaskAsync(token, calendarId, "pictogram:brush-teeth");
        Assert.NotNull(created);
        Assert.Equal("pictogram:brush-teeth", created.Icon);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Title = "Wash", Icon = "pictogram:wash-face-hands", Color = "#123456" })
                .ToUrl($"/calendars/{calendarId}/items/{created.Id}/details");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal("pictogram:wash-face-hands", response.ReadAsJson<CalendarItemDto>().Icon);
    }

    [Theory]
    [MemberData(nameof(MalformedPictograms))]
    public async Task Creating_an_item_with_a_malformed_pictogram_is_rejected(string icon)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        await CreateTaskAsync(token, calendarId, icon, expectedStatus: 400);
    }

    [Theory]
    [MemberData(nameof(MalformedPictograms))]
    public async Task Updating_an_item_with_a_malformed_pictogram_is_rejected(string icon)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");
        var item = await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId);
        Assert.NotNull(item);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Title = "Renamed", Icon = icon, Color = "#123456" })
                .ToUrl($"/calendars/{calendarId}/items/{item.Id}/details");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("icon", error.Details.Keys);
    }

    [Fact]
    public async Task An_unknown_pictogram_name_is_accepted_because_the_frontend_owns_the_set()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Personal");

        var created = await CreateTaskAsync(token, calendarId, "pictogram:unicorn");

        Assert.Equal("pictogram:unicorn", created!.Icon);
    }

    [Fact]
    public async Task A_routine_step_without_an_icon_inherits_the_scheduled_routines_pictogram()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Family");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Family", groupId);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{child.Id}");
            _.StatusCodeShouldBe(204);
        });

        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(
            fixture, guardianToken, child.Id, new CreateTaskTemplateOptions(Icon: "pictogram:wake-up"));
        Assert.NotNull(template);
        await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, template.Id, new AddSubtaskOptions("Brush teeth", "pictogram:brush-teeth", "00:05:00"));
        await TaskLibraryTestHelpers.AddSubtaskAsync(
            fixture, guardianToken, template.Id, new AddSubtaskOptions("Get ready", null, "00:05:00"));

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new
            {
                TaskTemplateId = template.Id,
                StartDate = startDate,
                StartTime = new TimeOnly(7, 0),
                AssignedTo = child.Id,
                Title = "Morning routine",
                Icon = "pictogram:wake-up",
                Color = "#ff0000",
            }).ToUrl($"/calendars/{calendarId}/items/from-template");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url($"/calendars/{calendarId}/occurrences?from={startDate:yyyy-MM-dd}&to={startDate:yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        var occurrences = response.ReadAsJson<CalendarItemOccurrenceDto[]>().OrderBy(o => o.StartsAt).ToArray();

        Assert.Equal(["pictogram:brush-teeth", "pictogram:wake-up"], occurrences.Select(o => o.Icon));
        Assert.All(occurrences, o => Assert.Equal("pictogram:wake-up", o.ParentIcon));
    }

    private async Task<CalendarItemDto?> CreateTaskAsync(string token, Guid calendarId, string icon, int expectedStatus = 200)
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new
            {
                Title = "Brush teeth",
                Icon = icon,
                Color = "#ff0000",
                Schedule = new { Kind = (int)CalendarItemKind.Task, DueDate = new { Date = day, Time = new TimeOnly(7, 0) }, IsAllDay = false },
            }).ToUrl($"/calendars/{calendarId}/items");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<CalendarItemDto>() : null;
    }
}
