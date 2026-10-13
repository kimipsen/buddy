using Alba;

using buddy.Features.TaskLibrary;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

namespace buddy.IntegrationTests.Features.TaskLibrary;

// A guardian with one child (who can sign in), the shape every packing-list test starts from.
internal sealed record ChecklistFamily(string GuardianToken, Guid GuardianId, ChildResponseDto Child, string ChildToken);

// Packing checklists and child suggestions (docs/backend/analysis/packing-checklists.md).
internal static class ChecklistTestHelpers
{
    public static async Task<ChecklistFamily> CreateFamilyAsync(BuddyApiFixture fixture, string childName = "Emil")
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, childName);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        return new ChecklistFamily(guardianToken, guardianId, child, childToken);
    }

    public static async Task<TaskTemplateDto> CreateChecklistAsync(
        BuddyApiFixture fixture, string guardianToken, Guid childId, string name = "School bag", string? eveningBeforeFrom = null)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Name = name, Icon = "backpack", Color = "#3355ff", Kind = "Checklist", EveningBeforeFrom = eveningBeforeFrom })
                .ToUrl($"/task-templates/children/{childId}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<TaskTemplateDto>();
    }

    public static async Task<TaskTemplateDto?> AddItemAsync(
        BuddyApiFixture fixture, string guardianToken, Guid templateId, string title, string[]? weekdays = null, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Json(new { Title = title, Weekdays = weekdays }).ToUrl($"/task-templates/{templateId}/subtasks");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<TaskTemplateDto>() : null;
    }

    public static async Task<TaskTemplateDto> ChecklistWithItemsAsync(BuddyApiFixture fixture, ChecklistFamily family, params string[] titles)
    {
        var template = await CreateChecklistAsync(fixture, family.GuardianToken, family.Child.Id);

        foreach (var title in titles)
        {
            template = (await AddItemAsync(fixture, family.GuardianToken, template.Id, title))!;
        }

        return template;
    }

    public static Task<IScenarioResult> SuggestAsync(
        BuddyApiFixture fixture, string token, Guid templateId, string title, string[]? weekdays = null, int expectedStatus = 200, string? idempotencyKey = null) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");

            if (idempotencyKey is not null)
            {
                _.WithRequestHeader("Idempotency-Key", idempotencyKey);
            }

            _.Post.Json(new { Title = title, Weekdays = weekdays }).ToUrl($"/task-templates/{templateId}/suggestions");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<Guid> SuggestIdAsync(BuddyApiFixture fixture, string childToken, Guid templateId, string title, string[]? weekdays = null)
    {
        var response = await SuggestAsync(fixture, childToken, templateId, title, weekdays);

        return response.ReadAsJson<TaskTemplateDto>().Suggestions.Single(s => s.Title == title && s.Status == "Pending").Id;
    }

    public static Task<IScenarioResult> ApproveAsync(
        BuddyApiFixture fixture, string token, Guid templateId, Guid suggestionId, string title, string[]? weekdays = null, int? position = null, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Title = title, Weekdays = weekdays, Position = position })
                .ToUrl($"/task-templates/{templateId}/suggestions/{suggestionId}/approve");
            _.StatusCodeShouldBe(expectedStatus);
        });

    // decline or withdraw, which take no body.
    public static Task<IScenarioResult> ResolveAsync(
        BuddyApiFixture fixture, string token, Guid templateId, Guid suggestionId, string action, int expectedStatus = 200) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url($"/task-templates/{templateId}/suggestions/{suggestionId}/{action}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<IReadOnlyList<TaskTemplateDto>> ListAsync(BuddyApiFixture fixture, string token, Guid childId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/task-templates/children/{childId}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<TaskTemplateDto[]>();
    }

    public static async Task<IReadOnlyCollection<TaskTemplateEvent>> ReadEventsAsync(BuddyApiFixture fixture, Guid templateId) =>
        await fixture.Host.Services.GetRequiredService<ITaskTemplateEventStore>().ReadAsync(new TaskTemplateId(templateId), CancellationToken.None);

    public static string ErrorCode(IScenarioResult response) => response.ReadAsJson<buddy.Common.ErrorEnvelope>().Code;

    // "Today" as the calendars the tests create see it (CalendarTestHelpers.DefaultTimeZone).
    public static DateOnly CalendarToday()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Calendars.CalendarTestHelpers.DefaultTimeZone);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date);
    }
}
