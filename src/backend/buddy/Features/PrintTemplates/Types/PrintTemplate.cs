namespace buddy.Features.PrintTemplates;

public sealed record PrintTemplate(
    PrintTemplateId Id,
    PrintTemplateOwner Owner,
    string Name,
    PaperSize PaperSize,
    DayOfWeek DefaultStartWeekday,
    bool ShowWeekNumber,
    IReadOnlyList<PrintTemplateRow> Rows,
    IReadOnlyList<GuardianColor> GuardianColors,
    bool IsDeleted = false)
{
    public static PrintTemplate? Rehydrate(IEnumerable<PrintTemplateEvent> events) => events.Aggregate((PrintTemplate?)null, Fold);

    // Single-event step, split out from Rehydrate so PrintTemplateSnapshotProjection can drive the
    // same logic one Marten-delivered event at a time. Deliberately not named Apply/Create -- those
    // names are a convention JasperFx's projection source generator scans for on any type used as
    // a projection document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static PrintTemplate? Fold(PrintTemplate? template, PrintTemplateEvent @event) => @event switch
    {
        PrintTemplateCreated created => Seed(created.Id, new PrintTemplateOwner.User(created.OwnerId), created.Name),
        PrintTemplateCreatedForGroup created => Seed(created.Id, new PrintTemplateOwner.Group(created.OwnerId), created.Name),
        PrintTemplateRenamed renamed => template! with { Name = renamed.After },
        PrintTemplateLayoutChanged changed => template! with
        {
            PaperSize = changed.PaperSizeAfter,
            DefaultStartWeekday = changed.StartWeekdayAfter,
            ShowWeekNumber = changed.ShowWeekNumberAfter
        },
        PrintTemplateRowsReplaced replaced => template! with { Rows = replaced.After },
        PrintTemplateGuardianColorsReplaced replaced => template! with { GuardianColors = replaced.After },
        PrintTemplateDeleted => template! with { IsDeleted = true },
    };

    // Defaults for a new template: A4, Monday start, week number shown, nothing configured yet.
    private static PrintTemplate Seed(PrintTemplateId id, PrintTemplateOwner owner, string name) =>
        new(id, owner, name, PaperSize.A4, DayOfWeek.Monday, ShowWeekNumber: true, Rows: [], GuardianColors: []);
}
