using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.Features.PrintTemplates;
using buddy.Features.Users;
using buddy.Features.WorkLocations;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class PrintTemplateEventShapeTests
{
    private static readonly PrintTemplateId FixedTemplateId = new(Guid.Parse("00000000-0000-0000-0000-000000000090"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly UserId FixedOtherGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly GroupId FixedGroupId = new(Guid.Parse("00000000-0000-0000-0000-000000000010"));
    private static readonly CalendarId FixedCalendarId = new(Guid.Parse("00000000-0000-0000-0000-000000000020"));
    private static readonly WorkLocationId FixedLocationId = new(Guid.Parse("00000000-0000-0000-0000-000000000080"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PrintTemplateCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PrintTemplateCreated(FixedTemplateId, FixedGuardianId, "Ugeplan", FixedGuardianId, FixedInstant),
        "PrintTemplates/PrintTemplateCreated.json");

    [Fact]
    public void PrintTemplateCreatedForGroup() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PrintTemplateCreatedForGroup(FixedTemplateId, FixedGroupId, "Ugeplan", FixedGuardianId, FixedInstant),
        "PrintTemplates/PrintTemplateCreatedForGroup.json");

    [Fact]
    public void PrintTemplateRenamed() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PrintTemplateRenamed(FixedTemplateId, "Ugeplan", "Skoleuge", FixedGuardianId, FixedInstant),
        "PrintTemplates/PrintTemplateRenamed.json");

    [Fact]
    public void PrintTemplateLayoutChanged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PrintTemplateLayoutChanged(
            FixedTemplateId, PaperSize.A4, PaperSize.A3, DayOfWeek.Monday, DayOfWeek.Sunday, true, false, FixedGuardianId, FixedInstant),
        "PrintTemplates/PrintTemplateLayoutChanged.json");

    // One event carrying every row kind, so the golden file pins each kind's field shape.
    [Fact]
    public void PrintTemplateRowsReplaced() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PrintTemplateRowsReplaced(
            FixedTemplateId,
            [],
            [
                new PrintTemplateRow(PrintRowKind.Meal, "Aftensmad", 1, ChildId: FixedChildId, MealSlot: MealSlot.Dinner),
                new PrintTemplateRow(PrintRowKind.Meal, "Frokost", 1, MealGroupId: FixedGroupId, MealSlot: MealSlot.Lunch),
                new PrintTemplateRow(PrintRowKind.Pickup, "Aflevere / Hente", 1, ChildId: FixedChildId),
                new PrintTemplateRow(PrintRowKind.WorkLocation, "Far på Stil", 1, GuardianId: FixedGuardianId, WorkLocationId: FixedLocationId),
                new PrintTemplateRow(PrintRowKind.WorkLocation, "Mor", 1, GuardianId: FixedOtherGuardianId),
                new PrintTemplateRow(PrintRowKind.CalendarMarker, "Affald", 1, CalendarIds: [FixedCalendarId], TitleFilter: "Skrald"),
                new PrintTemplateRow(
                    PrintRowKind.CalendarEvents, "Signes aktiviteter", 2, CalendarIds: [FixedCalendarId], AssignedToId: FixedChildId,
                    TitleFilter: null, MaxItems: 3, ShowTime: true, ShowAssignee: true),
                new PrintTemplateRow(PrintRowKind.TaskChecklist, "Viggos ansvar", 2, CalendarIds: [FixedCalendarId], AssignedToId: FixedChildId, MaxItems: 4),
                new PrintTemplateRow(PrintRowKind.Blank, "", 3),
            ],
            FixedGuardianId,
            FixedInstant),
        "PrintTemplates/PrintTemplateRowsReplaced.json");

    [Fact]
    public void PrintTemplateGuardianColorsReplaced() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PrintTemplateGuardianColorsReplaced(
            FixedTemplateId,
            [],
            [new GuardianColor(FixedGuardianId, new Color("#0ea5e9")), new GuardianColor(FixedOtherGuardianId, new Color("#f43f5e"))],
            FixedGuardianId,
            FixedInstant),
        "PrintTemplates/PrintTemplateGuardianColorsReplaced.json");

    [Fact]
    public void PrintTemplateDeleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new PrintTemplateDeleted(FixedTemplateId, FixedGuardianId, FixedInstant),
        "PrintTemplates/PrintTemplateDeleted.json");
}
