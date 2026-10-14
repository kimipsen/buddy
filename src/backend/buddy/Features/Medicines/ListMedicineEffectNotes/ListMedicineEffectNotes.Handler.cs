using buddy.Common;
using buddy.Common.Observability;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class ListMedicineEffectNotesHandler
{
    // The sleep diary's limit: no expansion here, only a snapshot filter and DoseLog lookups, and a
    // dose-adjustment conversation looks back over a few weeks either side of a change.
    public const int MaxRangeDays = 92;

    public static async Task<Result<MedicineEffectNotesResponse>> Handle(
        ListMedicineEffectNotes query,
        IValidator<ListMedicineEffectNotes> validator,
        IMedicineEffectLogEventStore effectLogs,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        ILogger<ListMedicineEffectNotes> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<MedicineEffectNotesResponse>.Validation(problem);
        }

        var access = await MedicineAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<MedicineEffectNotesResponse>();
        }

        var log = await effectLogs.FindSnapshotAsync(MedicineEffectLogId.ForChild(query.ChildId), cancellationToken);
        var notes = log?.Notes.Values
            .Where(n => n.Date >= query.From && n.Date <= query.To)
            .OrderBy(n => n.Date)
            .ToList() ?? [];

        var schedules = notes.Count == 0
            ? new Dictionary<MedicineId, MedicineSchedule>()
            : await MedicineEffectNotes.LoadSchedulesAsync(query.ChildId, medicines, cancellationToken);

        logger.MedicineEffectNotesRead(query.ChildId.Value, query.From, query.To, query.UserId.Value, HealthDataAccessPath.Guardian);

        return new Result<MedicineEffectNotesResponse>.Success(
            new MedicineEffectNotesResponse([.. notes.Select(n => MedicineEffectNoteResponse.From(n, schedules))]));
    }
}
