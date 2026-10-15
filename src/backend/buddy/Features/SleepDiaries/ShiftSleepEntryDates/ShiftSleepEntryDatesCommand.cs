using System.Globalization;

using buddy.Common.Concurrency;

using JasperFx.CommandLine;

using Marten;

namespace buddy.Features.SleepDiaries;

public sealed class ShiftSleepEntryDatesInput : NetCoreInput
{
    [Description("Entries last logged before this instant move one day back, e.g. 2026-10-15T12:00:00Z (when the \"Natten efter\" label went live)")]
    public string LoggedBefore { get; set; } = "";

    [Description("Append the events; without it the command only reports what it would do")]
    public bool ApplyFlag { get; set; }
}

// One-off: dotnet run --project src/backend/buddy -- shift-sleep-entry-dates <logged-before> [--apply]
// See SleepEntryDateShift for what moves.
[Description("One-off fix: move sleep entries logged under the old Danish \"Natten til\" label one day back", Name = "shift-sleep-entry-dates")]
public sealed class ShiftSleepEntryDatesCommand : JasperFxAsyncCommand<ShiftSleepEntryDatesInput>
{
    public override async Task<bool> Execute(ShiftSleepEntryDatesInput input)
    {
        if (!DateTimeOffset.TryParse(input.LoggedBefore, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var loggedBefore))
        {
            Console.Error.WriteLine($"Not an instant: '{input.LoggedBefore}'.");
            return false;
        }

        using var host = input.BuildHost();
        var store = host.Services.GetRequiredService<ISleepDiariesStore>();
        var diaries = host.Services.GetRequiredService<ISleepDiaryEventStore>();

        IReadOnlyList<Guid> ids;
        await using (var session = store.QuerySession())
        {
            ids = await session.Query<SleepDiarySnapshot>().Select(s => s.Id).ToListAsync();
        }

        var now = DateTimeOffset.UtcNow;
        var (moved, skipped) = (0, 0);
        Console.WriteLine($"{(input.ApplyFlag ? "Applying" : "Dry run")}: entries logged before {loggedBefore:O}, {ids.Count} diaries.");

        foreach (var id in ids)
        {
            // A scope makes the append expect the version ReadAsync saw, so a guardian saving a
            // night meanwhile fails this diary instead of being overwritten.
            using var scope = StreamVersionTracker.BeginScope();
            var diaryId = new SleepDiaryId(id);
            var plan = SleepEntryDateShift.For(await diaries.ReadAsync(diaryId, CancellationToken.None), loggedBefore, now);

            if (plan.Conflicts.Count > 0)
            {
                skipped++;
                Console.WriteLine($"  {id}: skipped, a newer entry already holds {string.Join(", ", plan.Conflicts.Select(d => d.ToString("O", CultureInfo.InvariantCulture)))}");
                continue;
            }

            if (plan.Moved == 0)
            {
                continue;
            }

            Console.WriteLine($"  {id}: {plan.Moved} entries move one day back ({plan.Events.Count} events)");
            if (input.ApplyFlag)
            {
                await diaries.AppendAsync(diaryId, plan.Events, CancellationToken.None);
            }

            moved += plan.Moved;
        }

        Console.WriteLine($"{moved} entries {(input.ApplyFlag ? "moved" : "would move")}; {skipped} diaries skipped for a conflict.");
        return true;
    }
}
