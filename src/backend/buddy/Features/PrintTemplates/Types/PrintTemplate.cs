using buddy.Common.Aggregates;

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
    // Not positional: snapshots stored before babysitter colors existed lack the property, and an
    // init property keeps its [] default where a missing constructor parameter would come back null.
    public IReadOnlyList<BabysitterColor> BabysitterColors { get; init; } = [];

    public static PrintTemplate? Rehydrate(IEnumerable<PrintTemplateEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static PrintTemplate Replay(IEnumerable<PrintTemplateEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // PrintTemplateSnapshotProjection can drive the same logic one Marten-delivered event at a
    // time. Deliberately not named Apply/Create -- those names are a convention JasperFx's
    // projection source generator scans for on any type used as a projection document (see Question
    // 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static PrintTemplate Start(PrintTemplateEvent @event) => @event switch
    {
        PrintTemplateCreated created => Seed(created.Id, new PrintTemplateOwner.User(created.OwnerId), created.Name),
        PrintTemplateCreatedForGroup created => Seed(created.Id, new PrintTemplateOwner.Group(created.OwnerId), created.Name),
        _ => throw EventReplay.NotAStartEvent(nameof(PrintTemplate), @event.EventType)
    };

    public static PrintTemplate Advance(PrintTemplate template, PrintTemplateEvent @event) => @event switch
    {
        PrintTemplateRenamed renamed => template with { Name = renamed.After },
        PrintTemplateLayoutChanged changed => template with
        {
            PaperSize = changed.PaperSizeAfter,
            DefaultStartWeekday = changed.StartWeekdayAfter,
            ShowWeekNumber = changed.ShowWeekNumberAfter
        },
        PrintTemplateRowsReplaced replaced => template with { Rows = replaced.After },
        PrintTemplateGuardianColorsReplaced replaced => template with { GuardianColors = replaced.After },
        PrintTemplateBabysitterColorsReplaced replaced => template with { BabysitterColors = replaced.After },
        PrintTemplateDeleted => template with { IsDeleted = true },
        PrintTemplateCreated or PrintTemplateCreatedForGroup => throw EventReplay.AlreadyStarted(nameof(PrintTemplate), @event.EventType)
    };

    // Defaults for a new template: A4, Monday start, week number shown, nothing configured yet.
    private static PrintTemplate Seed(PrintTemplateId id, PrintTemplateOwner owner, string name) =>
        new(id, owner, name, PaperSize.A4, DayOfWeek.Monday, ShowWeekNumber: true, Rows: [], GuardianColors: []);
}
