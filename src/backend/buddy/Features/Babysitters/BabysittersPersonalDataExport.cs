using buddy.Common.Erasure;

namespace buddy.Features.Babysitters;

// The "babysitters" section: the caller's babysitter list, archived babysitters included.
public sealed class BabysittersPersonalDataExporter(IBabysitterListEventStore lists) : IPersonalDataExporter
{
    public Type Store => typeof(IBabysittersStore);

    public string Section => "babysitters";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        var list = await lists.FindSnapshotAsync(BabysitterListId.ForGuardian(subject.UserId), cancellationToken);
        return list?.Babysitters.Select(BabysitterSummary.From).ToArray() ?? [];
    }
}
