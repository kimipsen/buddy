namespace buddy.Features.Progress;

public static class GetMyProgressHandler
{
    public static async Task<ProgressSummary> Handle(GetMyProgress query, IProgressEventStore progress, CancellationToken cancellationToken)
    {
        if (query.ChildId is not { } childId)
        {
            return ProgressSummary.From(null);
        }

        var id = ProgressId.ForChild(childId);
        var current = await progress.FindSnapshotAsync(id, cancellationToken);

        return ProgressSummary.From(current);
    }
}
