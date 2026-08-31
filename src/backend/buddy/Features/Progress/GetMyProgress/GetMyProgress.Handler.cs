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
        var events = await progress.ReadAsync(id, cancellationToken);
        var current = ChildProgress.Rehydrate(events);

        return ProgressSummary.From(current);
    }
}
