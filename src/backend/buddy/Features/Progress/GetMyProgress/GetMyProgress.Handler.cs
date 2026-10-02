namespace buddy.Features.Progress;

public static class GetMyProgressHandler
{
    public static async Task<ProgressSummary> Handle(GetMyProgress query, IProgressEventStore progress, CancellationToken cancellationToken)
    {
        var childId = query.ChildId;

        var id = ProgressId.ForChild(childId);
        var current = await progress.FindSnapshotAsync(id, cancellationToken) ?? ChildProgress.Initial(id, childId);

        return ProgressSummary.From(current);
    }
}
