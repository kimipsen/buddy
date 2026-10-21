using buddy.Common.FeatureFlags;
using buddy.Features.Users;
using buddy.Features.VisitSummaries;

namespace buddy.Features.Medicines;

// The medicine section of a doctor visit summary: the schedules the child was on during the period,
// every dose in it, and the effect notes. A medicine stopped during the period keeps its taken and
// skipped doses, because a dose change is what the doctor looks for
// (docs/backend/analysis/doctor-visit-summary.md, Question 6). A medicine the guardian left out of
// the effect summaries is removed here, on the server, so the doctor's shared link never receives
// it; only how many were left out is kept (docs/backend/analysis/medicine-effect-inclusion.md,
// Question 3).
public sealed class MedicineVisitSummarySource(IMedicineEventStore medicines, IMedicineEffectLogEventStore effectLogs) : IVisitSummarySectionSource
{
    public VisitSummarySection Section => VisitSummarySection.Medicine;

    public bool IsOffered(InstallationFeatures features) => features.Medicines;

    public async Task<VisitSummarySectionContent> ReadAsync(UserId childId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var all = await MedicineEffectNotes.LoadSchedulesAsync(childId, medicines, cancellationToken);
        var excluded = all.Values.Where(s => s.IsExcludedFromEffects).Select(s => s.Id).ToHashSet();
        var schedules = all.Values.Where(s => !excluded.Contains(s.Id)).ToList();

        var doses = MedicineDoseExpansion.Expand(schedules, from, to, includeStoppedHistory: true);
        var withDoses = doses.Select(d => d.MedicineId).ToHashSet();

        var log = await effectLogs.FindSnapshotAsync(MedicineEffectLogId.ForChild(childId), cancellationToken);
        var notesInPeriod = log?.Notes.Values
            .Where(n => n.Date >= from && n.Date <= to)
            .OrderBy(n => n.Date)
            .ToList() ?? [];

        // Left out and present in the period: a dose in it, or named in a note's regimen.
        var excludedDoses = MedicineDoseExpansion.Expand(
            all.Values.Where(s => excluded.Contains(s.Id)), from, to, includeStoppedHistory: true);
        var excludedInPeriod = excludedDoses.Select(d => d.MedicineId)
            .Concat(notesInPeriod.SelectMany(n => n.Regimen).Select(m => m.MedicineId).Where(excluded.Contains))
            .ToHashSet();

        var notes = notesInPeriod
            .Select(n => MedicineEffectNoteResponse.From(
                n with { Regimen = [.. n.Regimen.Where(m => !excluded.Contains(m.MedicineId))] },
                all))
            .ToList();

        return new VisitSummaryMedicineSection(
            [.. schedules
                .Where(s => withDoses.Contains(s.Id))
                .OrderBy(s => s.StartDate)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .Select(MedicineScheduleResponse.FromSchedule)],
            [.. doses],
            notes,
            excludedInPeriod.Count);
    }
}

// Schedules: only those with a dose in the period, stopped ones included (IsStopped says which).
// Medicines left out of the effect summaries are missing from all three lists;
// ExcludedMedicineCount says how many of them the period had, without naming them.
public sealed record VisitSummaryMedicineSection(
    IReadOnlyList<MedicineScheduleResponse> Schedules,
    IReadOnlyList<MedicineDoseOccurrence> Doses,
    IReadOnlyList<MedicineEffectNoteResponse> EffectNotes,
    int ExcludedMedicineCount);
