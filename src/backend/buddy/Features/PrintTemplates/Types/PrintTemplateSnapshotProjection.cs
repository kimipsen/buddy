using Marten.Events.Aggregation;

namespace buddy.Features.PrintTemplates;

// Wrapper document for the same reason as PickupScheduleSnapshot: Marten can't use a sealed-record
// id class as a document Id. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record PrintTemplateSnapshot(Guid Id, PrintTemplate PrintTemplate);

// Inline snapshot of PrintTemplate in the shared "snapshots" schema -- derived, rebuildable state.
public sealed class PrintTemplateSnapshotProjection : SingleStreamProjection<PrintTemplateSnapshot, Guid>
{
    public static PrintTemplateSnapshot Create(PrintTemplateCreated created) =>
        new(created.Id.Value, PrintTemplate.Start(PrintTemplateEvent.FromPayload(created)));

    public static PrintTemplateSnapshot Create(PrintTemplateCreatedForGroup created) =>
        new(created.Id.Value, PrintTemplate.Start(PrintTemplateEvent.FromPayload(created)));

    public PrintTemplateSnapshot Apply(PrintTemplateSnapshot current, PrintTemplateRenamed e) => Next(current, e);

    public PrintTemplateSnapshot Apply(PrintTemplateSnapshot current, PrintTemplateLayoutChanged e) => Next(current, e);

    public PrintTemplateSnapshot Apply(PrintTemplateSnapshot current, PrintTemplateRowsReplaced e) => Next(current, e);

    public PrintTemplateSnapshot Apply(PrintTemplateSnapshot current, PrintTemplateGuardianColorsReplaced e) => Next(current, e);

    public PrintTemplateSnapshot Apply(PrintTemplateSnapshot current, PrintTemplateDeleted e) => Next(current, e);

    private static PrintTemplateSnapshot Next(PrintTemplateSnapshot current, PrintTemplateEvent e) =>
        current with { PrintTemplate = PrintTemplate.Advance(current.PrintTemplate, e) };
}
