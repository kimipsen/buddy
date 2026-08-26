namespace buddy.Features.Progress;

public static class GetMyProgressHandler
{
    public static async Task<ProgressSummary> Handle(GetMyProgress query, IProgressEventStore progress, CancellationToken cancellationToken)
    {
        if (query.ChildId is not { } childId)
        {
            return new ProgressSummary(0, []);
        }

        var id = ProgressId.ForChild(childId);
        var events = await progress.ReadAsync(id, cancellationToken);
        var current = ChildProgress.Rehydrate(events);

        // No stream yet just means the child hasn't completed anything -- same "zero, not an
        // error" shape as any other never-started counter, not a NotFound.
        return current is null
            ? new ProgressSummary(0, [])
            : new ProgressSummary(current.TotalStars, [.. current.UnlockedMilestones.OrderBy(t => t)]);
    }
}

public sealed record ProgressSummary(int TotalStars, IReadOnlyList<int> UnlockedMilestones);
