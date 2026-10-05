using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

// What ListSleepDiaryEntries and GetSharedSleepDiary both read: the diary-wide notes plus the
// logged days in a range, sparse (a date with no entry is simply absent).
public sealed record SleepDiaryRange(string SleepHygieneNotes, IReadOnlyList<DatedSleepEntry> Entries)
{
    public static SleepDiaryRange From(SleepDiary? diary, DateOnly from, DateOnly to) => diary is null
        ? new SleepDiaryRange("", [])
        : new SleepDiaryRange(
            diary.SleepHygieneNotes,
            [.. diary.Entries
                .Where(e => e.Key >= from && e.Key <= to)
                .OrderBy(e => e.Key)
                .Select(e => new DatedSleepEntry(e.Key, e.Value))]);
}

public sealed record DatedSleepEntry(DateOnly Date, SleepEntry Entry);

// Durations cross the wire as whole minutes -- what the form inputs edit -- while the domain and the
// persisted events keep TimeSpan, as the design doc specifies.
public sealed record SleepIntervalDto(TimeOnly StartTime, int DurationMinutes)
{
    public static SleepIntervalDto From(SleepInterval interval) => new(interval.StartTime, ToMinutes(interval.Duration));

    public SleepInterval ToDomain() => new(StartTime, TimeSpan.FromMinutes(DurationMinutes));

    internal static int ToMinutes(TimeSpan duration) => (int)Math.Round(duration.TotalMinutes);
}

public sealed record SleepEntryResponse(
    DateOnly Date,
    // Derived, never stored (the paper form's "weekend" column).
    bool IsWeekend,
    TimeOnly? RoutineStartTime,
    TimeOnly? RitualStartTime,
    TimeOnly? RitualEndTime,
    TimeOnly? BedTime,
    TimeOnly? FellAsleepTime,
    IReadOnlyList<SleepIntervalDto> NightWakeUps,
    TimeOnly? MorningWakeTime,
    bool IsTired,
    IReadOnlyList<SleepIntervalDto> Naps,
    int? TotalSleepMinutes,
    string Remarks,
    Guid LoggedBy)
{
    public static SleepEntryResponse From(DateOnly date, SleepEntry entry) => new(
        date,
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        entry.RoutineStartTime,
        entry.RitualStartTime,
        entry.RitualEndTime,
        entry.BedTime,
        entry.FellAsleepTime,
        [.. entry.NightWakeUps.Select(SleepIntervalDto.From)],
        entry.MorningWakeTime,
        entry.IsTired,
        [.. entry.Naps.Select(SleepIntervalDto.From)],
        entry.TotalSleepDuration is { } total ? SleepIntervalDto.ToMinutes(total) : null,
        entry.Remarks,
        entry.LoggedBy.Value);

    public static SleepEntryResponse From(DatedSleepEntry dated) => From(dated.Date, dated.Entry);
}

// A link as the guardian sees it in the list -- never the plaintext token, which only
// CreateSleepDiaryShareLink returns, once.
public sealed record SleepDiaryShareLinkSummary(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt)
{
    public static SleepDiaryShareLinkSummary From(SleepDiaryShareTokenDocument document) =>
        new(document.Id, document.CreatedAt, document.ExpiresAt);
}

internal static class SleepDiaryShareLinks
{
    public static bool IsLive(SleepDiaryShareTokenDocument document, DateTimeOffset now) =>
        !document.IsRevoked && (document.ExpiresAt is null || document.ExpiresAt > now);
}

public sealed record SharedSleepDiary(Name ChildName, DateOnly From, DateOnly To, DateTimeOffset? ExpiresAt, SleepDiaryRange Diary);

// The plaintext token is returned exactly once, here.
public sealed record IssuedSleepDiaryShareLink(SleepDiaryShareTokenId Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

