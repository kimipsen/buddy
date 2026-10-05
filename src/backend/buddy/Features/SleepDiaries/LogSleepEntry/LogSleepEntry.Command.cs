using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record LogSleepEntry(
    UserId UserId,
    UserId ChildId,
    DateOnly Date,
    TimeOnly? RoutineStartTime,
    TimeOnly? RitualStartTime,
    TimeOnly? RitualEndTime,
    TimeOnly? BedTime,
    TimeOnly? FellAsleepTime,
    IReadOnlyList<SleepInterval> NightWakeUps,
    TimeOnly? MorningWakeTime,
    bool IsTired,
    IReadOnlyList<SleepInterval> Naps,
    TimeSpan? TotalSleepDuration,
    string Remarks)
{
    public static LogSleepEntry FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly date, LogSleepEntryRequest request) =>
        new(
            principal.GetRequiredUserId(),
            childId,
            date,
            request.RoutineStartTime,
            request.RitualStartTime,
            request.RitualEndTime,
            request.BedTime,
            request.FellAsleepTime,
            [.. (request.NightWakeUps ?? []).Select(i => i.ToDomain())],
            request.MorningWakeTime,
            request.IsTired,
            [.. (request.Naps ?? []).Select(i => i.ToDomain())],
            request.TotalSleepMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            FreeText.Normalize(request.Remarks));

    public SleepEntry ToEntry() => new(
        RoutineStartTime,
        RitualStartTime,
        RitualEndTime,
        BedTime,
        FellAsleepTime,
        NightWakeUps,
        MorningWakeTime,
        IsTired,
        Naps,
        TotalSleepDuration,
        Remarks,
        UserId);
}
