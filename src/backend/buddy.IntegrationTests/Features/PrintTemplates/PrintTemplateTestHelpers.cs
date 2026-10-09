using Alba;

using buddy.Features.Mealplans;
using buddy.Features.PrintTemplates;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.PrintTemplates;

// Response shapes, matching PrintTemplateResponse / PrintTemplateSummary
// (Features/PrintTemplates/Types/PrintTemplateResponse.cs). Enums travel as their member names.
internal sealed record PrintTemplateRowDto(
    PrintRowKind Kind,
    string Label,
    int HeightWeight,
    Guid? ChildId,
    Guid? MealGroupId,
    MealSlot? MealSlot,
    Guid? GuardianId,
    Guid? WorkLocationId,
    List<Guid>? CalendarIds,
    Guid? AssignedToId,
    string? TitleFilter,
    int? MaxItems,
    bool ShowTime,
    bool ShowAssignee);

internal sealed record GuardianColorDto(Guid GuardianId, string Color);

internal sealed record BabysitterColorDto(Guid GuardianId, Guid BabysitterId, string Color);

internal sealed record PrintTemplateDto(
    Guid Id,
    Guid? OwnerUserId,
    Guid? OwnerGroupId,
    string Name,
    PaperSize PaperSize,
    DayOfWeek DefaultStartWeekday,
    bool ShowWeekNumber,
    List<PrintTemplateRowDto> Rows,
    List<GuardianColorDto> GuardianColors,
    List<BabysitterColorDto> BabysitterColors);

internal sealed record PrintTemplateSummaryDto(Guid Id, Guid? OwnerUserId, Guid? OwnerGroupId, string Name);

internal static class PrintTemplateTestHelpers
{
    // Row kinds, sent and read back by member name.
    public const PrintRowKind Meal = PrintRowKind.Meal;
    public const PrintRowKind Pickup = PrintRowKind.Pickup;
    public const PrintRowKind WorkLocation = PrintRowKind.WorkLocation;
    public const PrintRowKind CalendarMarker = PrintRowKind.CalendarMarker;
    public const PrintRowKind CalendarEvents = PrintRowKind.CalendarEvents;
    public const PrintRowKind TaskChecklist = PrintRowKind.TaskChecklist;
    public const PrintRowKind Blank = PrintRowKind.Blank;

    public const MealSlot Dinner = MealSlot.Dinner;

    public static async Task<PrintTemplateDto> CreateAsync(BuddyApiFixture fixture, string token, string name = "Ugeplan", Guid? groupId = null)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name, GroupId = groupId }).ToUrl("/print-templates");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<PrintTemplateDto>();
    }

    public static async Task<PrintTemplateDto> GetAsync(BuddyApiFixture fixture, string token, Guid templateId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/print-templates/{templateId}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<PrintTemplateDto>();
    }

    public static async Task<List<PrintTemplateSummaryDto>> ListAsync(BuddyApiFixture fixture, string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/print-templates");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<PrintTemplateSummaryDto>>();
    }

    public static async Task<IScenarioResult> ReplaceRowsAsync(BuddyApiFixture fixture, string token, Guid templateId, object[] rows, int expectedStatus = 200)
    {
        return await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Rows = rows }).ToUrl($"/print-templates/{templateId}/rows");
            _.StatusCodeShouldBe(expectedStatus);
        });
    }

    public static object BlankRow(string label = "") => new { Kind = Blank, Label = label, HeightWeight = 1 };
}
