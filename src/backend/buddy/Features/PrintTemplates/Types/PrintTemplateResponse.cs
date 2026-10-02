namespace buddy.Features.PrintTemplates;

// HTTP shapes. Rows and colors go out as their domain records: strongly-typed ids flatten to bare
// Guids and enums to ordinals, like PickupOccurrence.
public sealed record PrintTemplateResponse(
    Guid Id,
    Guid? OwnerUserId,
    Guid? OwnerGroupId,
    string Name,
    PaperSize PaperSize,
    DayOfWeek DefaultStartWeekday,
    bool ShowWeekNumber,
    IReadOnlyList<PrintTemplateRow> Rows,
    IReadOnlyList<GuardianColor> GuardianColors)
{
    public static PrintTemplateResponse From(PrintTemplate template)
    {
        var index = PrintTemplateIndexDocument.From(template);

        return new(
            template.Id.Value,
            index.OwnerUserId,
            index.OwnerGroupId,
            template.Name,
            template.PaperSize,
            template.DefaultStartWeekday,
            template.ShowWeekNumber,
            template.Rows,
            template.GuardianColors);
    }
}

public sealed record PrintTemplateSummary(Guid Id, Guid? OwnerUserId, Guid? OwnerGroupId, string Name)
{
    public static PrintTemplateSummary From(PrintTemplateIndexDocument index) =>
        new(index.Id, index.OwnerUserId, index.OwnerGroupId, index.Name);
}
