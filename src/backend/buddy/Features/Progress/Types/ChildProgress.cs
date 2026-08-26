using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record ChildProgress(
    ProgressId Id,
    UserId ChildId,
    int TotalStars,
    ImmutableHashSet<(CalendarItemId ItemId, DateOnly OccurrenceDate)> AwardedOccurrences,
    ImmutableHashSet<int> UnlockedMilestones)
{
    public static ChildProgress? Rehydrate(IEnumerable<ProgressEvent> events)
    {
        ChildProgress? progress = null;

        foreach (var @event in events)
        {
            progress = @event switch
            {
                ProgressStarted started => new ChildProgress(
                    started.Id,
                    started.ChildId,
                    0,
                    ImmutableHashSet<(CalendarItemId, DateOnly)>.Empty,
                    ImmutableHashSet<int>.Empty),
                StarAwarded awarded => progress! with
                {
                    TotalStars = progress!.TotalStars + 1,
                    AwardedOccurrences = progress!.AwardedOccurrences.Add((awarded.SourceItemId, awarded.OccurrenceDate))
                },
                StarRevoked revoked => progress! with
                {
                    TotalStars = progress!.TotalStars - 1,
                    AwardedOccurrences = progress!.AwardedOccurrences.Remove((revoked.SourceItemId, revoked.OccurrenceDate))
                },
                MilestoneUnlocked milestone => progress! with
                {
                    UnlockedMilestones = progress!.UnlockedMilestones.Add(milestone.Threshold)
                },
                _ => progress
            };
        }

        return progress;
    }
}
