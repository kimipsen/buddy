using System.Collections.Immutable;

namespace buddy.Features.Progress;

public static class RecordStarChangeHandler
{
    public static async Task Handle(RecordStarChange command, IProgressEventStore progress, CancellationToken cancellationToken)
    {
        var id = ProgressId.ForChild(command.ChildId);
        var existingEvents = await progress.ReadAsync(id, cancellationToken);
        var current = ChildProgress.Rehydrate(existingEvents);

        var occurrence = (command.ItemId, command.OccurrenceDate, command.SubtaskId);
        var alreadyAwarded = current?.AwardedOccurrences.Contains(occurrence) ?? false;

        // Mirrors SetTaskCompletionHandler's own before == after guard -- nothing changed for
        // Progress either, so append nothing.
        if (command.IsCompleted == alreadyAwarded)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var newEvents = new List<ProgressEvent>();

        if (current is null)
        {
            newEvents.Add(new ProgressStarted(id, command.ChildId, now));
        }

        if (command.IsCompleted)
        {
            newEvents.Add(new StarAwarded(id, command.ItemId, command.OccurrenceDate, now, command.SubtaskId));

            var totalAfter = (current?.TotalStars ?? 0) + 1;
            var alreadyUnlocked = current?.UnlockedMilestones ?? ImmutableHashSet<int>.Empty;
            var configuredGoalPosts = current?.GoalPosts ?? ImmutableArray<GoalPost>.Empty;
            var crossed = GoalPostResolver.AtThreshold(configuredGoalPosts, totalAfter);

            if (crossed is not null && !alreadyUnlocked.Contains(crossed.Threshold))
            {
                newEvents.Add(new MilestoneUnlocked(id, crossed.Threshold, now));
            }
        }
        else
        {
            newEvents.Add(new StarRevoked(id, command.ItemId, command.OccurrenceDate, now, command.SubtaskId));
        }

        if (current is null)
        {
            await progress.CreateAsync(id, newEvents, cancellationToken);
        }
        else
        {
            await progress.AppendAsync(id, newEvents, cancellationToken);
        }
    }
}
