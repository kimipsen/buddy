using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

// A child's warning settings are theirs alone, keyed by their UserId: erasing the child deletes the
// stream. An erased guardian's ModifiedBy stays as a pseudonym, as CreatedBy does on calendar items.
public sealed class TransitionWarningsPersonalDataEraser(ITransitionWarningsStore store) : IPersonalDataEraser
{
    public Type Store => typeof(ITransitionWarningsStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<TransitionWarningSettingsSnapshot>(TransitionWarningSettingsId.ForChild(child.UserId).Value, cancellationToken);
}

// The "transitionWarnings" section: each child's settings and per-item overrides (item ids only;
// the items themselves are in the calendars section).
public sealed class TransitionWarningsPersonalDataExporter(ITransitionWarningSettingsEventStore settings) : IPersonalDataExporter
{
    public Type Store => typeof(ITransitionWarningsStore);

    public string Section => "transitionWarnings";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedTransitionWarnings> children = [];

        foreach (var childId in subject.Children)
        {
            var current = await settings.FindSnapshotAsync(TransitionWarningSettingsId.ForChild(childId), cancellationToken)
                ?? TransitionWarningSettings.ForChild(childId);
            children.Add(new ExportedTransitionWarnings(childId.Value, MyTransitionWarningsResponse.From(current)));
        }

        return children;
    }
}

public sealed record ExportedTransitionWarnings(Guid ChildId, MyTransitionWarningsResponse Settings);
