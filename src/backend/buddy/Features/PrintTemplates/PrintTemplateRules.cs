using FluentValidation;

namespace buddy.Features.PrintTemplates;

// Structural limits from docs/backend/analysis/week-plan-print-templates.md#validation-limits.
internal static class PrintTemplateRules
{
    public const int MaxNameLength = 80;
    public const int MaxRows = 12;
    public const int MaxLabelLength = 40;
    public const int MinHeightWeight = 1;
    public const int MaxHeightWeight = 5;
    public const int MaxCalendarsPerRow = 10;
    public const int MaxTitleFilterLength = 60;
    public const int MaxItemsLimit = 8;
    public const int MaxGuardianColors = 12;
    public const int MaxColorLength = 32;

    public static void ValidTemplateName<T>(this AbstractValidator<T> validator, Func<T, string> name) =>
        validator.RuleFor(x => name(x).Trim())
            .NotEmpty()
            .WithMessage("A print template requires a name.")
            .MaximumLength(MaxNameLength)
            .OverridePropertyName("name");
}

// What each kind requires and allows. Anything a kind doesn't use must be null/false: stray values
// are rejected rather than silently dropped, so a stored row always says exactly what it means.
public sealed class PrintTemplateRowValidator : AbstractValidator<PrintTemplateRow>
{
    private static readonly PrintRowKind[] CalendarKinds = [PrintRowKind.CalendarMarker, PrintRowKind.CalendarEvents, PrintRowKind.TaskChecklist];

    // Every rule names its field explicitly in camelCase, so error keys match the JSON field names
    // whether a rule targets one property or the whole row (rows[0].childId, never rows[0].ChildId).
    public PrintTemplateRowValidator()
    {
        RuleFor(r => r.Kind).IsInEnum().OverridePropertyName("kind");

        RuleFor(r => r.Label)
            .Must(label => label.Trim().Length <= PrintTemplateRules.MaxLabelLength)
            .WithMessage($"A row label can be at most {PrintTemplateRules.MaxLabelLength} characters.")
            .Must(label => !string.IsNullOrWhiteSpace(label))
            .When(r => r.Kind != PrintRowKind.Blank, ApplyConditionTo.CurrentValidator)
            .WithMessage("Every row except a blank one needs a label.")
            .OverridePropertyName("label");

        RuleFor(r => r.HeightWeight)
            .InclusiveBetween(PrintTemplateRules.MinHeightWeight, PrintTemplateRules.MaxHeightWeight)
            .OverridePropertyName("heightWeight");

        RuleFor(r => r)
            .Must(r => (r.ChildId is null) != (r.MealGroupId is null))
            .WithMessage("A meal row needs exactly one of childId or mealGroupId.")
            .OverridePropertyName("childId")
            .When(r => r.Kind == PrintRowKind.Meal);

        RuleFor(r => r.MealSlot)
            .NotNull()
            .IsInEnum()
            .OverridePropertyName("mealSlot")
            .When(r => r.Kind == PrintRowKind.Meal);

        RuleFor(r => r.ChildId)
            .NotNull()
            .WithMessage("A pickup row needs childId.")
            .OverridePropertyName("childId")
            .When(r => r.Kind == PrintRowKind.Pickup);

        RuleFor(r => r.GuardianId)
            .NotNull()
            .WithMessage("A work location row needs guardianId.")
            .OverridePropertyName("guardianId")
            .When(r => r.Kind == PrintRowKind.WorkLocation);

        RuleFor(r => r.CalendarIds)
            .Must(ids => ids is { Count: > 0 and <= PrintTemplateRules.MaxCalendarsPerRow }
                && ids.All(id => id is not null)
                && ids.Distinct().Count() == ids.Count)
            .WithMessage($"This row needs 1–{PrintTemplateRules.MaxCalendarsPerRow} distinct calendarIds.")
            .OverridePropertyName("calendarIds")
            .When(r => CalendarKinds.Contains(r.Kind));

        RuleFor(r => r.TitleFilter)
            .Must(filter => !string.IsNullOrWhiteSpace(filter) && filter.Trim().Length <= PrintTemplateRules.MaxTitleFilterLength)
            .WithMessage($"titleFilter must be 1–{PrintTemplateRules.MaxTitleFilterLength} characters when given.")
            .OverridePropertyName("titleFilter")
            .When(r => r.TitleFilter is not null);

        RuleFor(r => r.MaxItems)
            .InclusiveBetween(1, PrintTemplateRules.MaxItemsLimit)
            .OverridePropertyName("maxItems")
            .When(r => r.MaxItems is not null);

        RuleFor(r => r)
            .Must(r => StrayFields(r).Count == 0)
            .WithMessage(r => $"A {r.Kind} row can't set: {string.Join(", ", StrayFields(r))}.")
            .OverridePropertyName("kind");
    }

    private static List<string> StrayFields(PrintTemplateRow row)
    {
        var allowed = row.Kind switch
        {
            PrintRowKind.Meal => new[] { "childId", "mealGroupId", "mealSlot" },
            PrintRowKind.Pickup => ["childId"],
            PrintRowKind.WorkLocation => ["guardianId", "workLocationId"],
            PrintRowKind.CalendarMarker => ["calendarIds", "titleFilter"],
            PrintRowKind.CalendarEvents => ["calendarIds", "assignedToId", "titleFilter", "maxItems", "showTime", "showAssignee"],
            PrintRowKind.TaskChecklist => ["calendarIds", "assignedToId", "maxItems"],
            _ => [],
        };

        var set = new (string Name, bool IsSet)[]
        {
            ("childId", row.ChildId is not null),
            ("mealGroupId", row.MealGroupId is not null),
            ("mealSlot", row.MealSlot is not null),
            ("guardianId", row.GuardianId is not null),
            ("workLocationId", row.WorkLocationId is not null),
            ("calendarIds", row.CalendarIds is not null),
            ("assignedToId", row.AssignedToId is not null),
            ("titleFilter", row.TitleFilter is not null),
            ("maxItems", row.MaxItems is not null),
            ("showTime", row.ShowTime),
            ("showAssignee", row.ShowAssignee),
        };

        return [.. set.Where(field => field.IsSet && !allowed.Contains(field.Name)).Select(field => field.Name)];
    }
}
