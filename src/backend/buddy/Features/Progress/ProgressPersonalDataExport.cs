using buddy.Common.Erasure;

namespace buddy.Features.Progress;

// The "progress" section: each child's stars, unlocked milestones and goal posts.
public sealed class ProgressPersonalDataExporter(IProgressEventStore progress) : IPersonalDataExporter
{
    public Type Store => typeof(IProgressStore);

    public string Section => "progress";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedProgress> children = [];

        foreach (var childId in subject.Children)
        {
            var id = ProgressId.ForChild(childId);
            var current = await progress.FindSnapshotAsync(id, cancellationToken) ?? ChildProgress.Initial(id, childId);
            children.Add(new ExportedProgress(childId.Value, ProgressSummary.From(current)));
        }

        return children;
    }
}

public sealed record ExportedProgress(Guid ChildId, ProgressSummary Progress);
