using buddy.Common.FeatureFlags;
using buddy.Features.Users;
using buddy.Features.VisitSummaries;

namespace buddy.Features.Medicines;

// The medicine section of a doctor visit summary: the schedules the child was on during the period,
// every dose in it, and the effect notes. A medicine stopped during the period keeps its taken and
// skipped doses, because a dose change is what the doctor looks for
// (docs/backend/analysis/doctor-visit-summary.md, Question 6).
public sealed class MedicineVisitSummarySource(IMedicineEventStore medicines, IMedicineEffectLogEventStore effectLogs) : IVisitSummarySectionSource
{
    public VisitSummarySection Section => VisitSummarySection.Medicine;

    public bool IsOffered(InstallationFeatures features) => features.Medicines;

    public async Task<VisitSummarySectionContent> ReadAsync(UserId childId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var schedules = await MedicineEffectNotes.LoadSchedulesAsync(childId, medicines, cancellationToken);
        var doses = MedicineDoseExpansion.Expand(schedules.Values, from, to, includeStoppedHistory: true);
        var withDoses = doses.Select(d => d.MedicineId).ToHashSet();

        var log = await effectLogs.FindSnapshotAsync(MedicineEffectLogId.ForChild(childId), cancellationToken);
        var notes = log?.Notes.Values
            .Where(n => n.Date >= from && n.Date <= to)
            .OrderBy(n => n.Date)
            .Select(n => MedicineEffectNoteResponse.From(n, schedules))
            .ToList() ?? [];

        return new VisitSummaryMedicineSection(
            [.. schedules.Values
                .Where(s => withDoses.Contains(s.Id))
                .OrderBy(s => s.StartDate)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .Select(MedicineScheduleResponse.FromSchedule)],
            [.. doses],
            notes);
    }
}

// Schedules: only those with a dose in the period, stopped ones included (IsStopped says which).
public sealed record VisitSummaryMedicineSection(
    IReadOnlyList<MedicineScheduleResponse> Schedules,
    IReadOnlyList<MedicineDoseOccurrence> Doses,
    IReadOnlyList<MedicineEffectNoteResponse> EffectNotes);
