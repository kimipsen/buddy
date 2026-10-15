namespace buddy.Features.Medicines;

public sealed record MedicineEffectNotesResponse(IReadOnlyList<MedicineEffectNoteResponse> Notes);

public sealed record MedicineEffectNoteResponse(
    DateOnly Date,
    IReadOnlyList<EffectRating> Effects,
    IReadOnlyList<SideEffectRating> SideEffects,
    TimeOnly? WoreOffAt,
    string Note,
    IReadOnlyList<RegimenMedicineResponse> Regimen,
    Guid LoggedBy)
{
    // The regimen's name and dosage come from the note (as they were that day); the dose statuses
    // come live from the schedules, so a dose marked after the note was saved still shows.
    public static MedicineEffectNoteResponse From(MedicineEffectNote note, IReadOnlyDictionary<MedicineId, MedicineSchedule> schedules) =>
        new(
            note.Date,
            note.Effects,
            note.SideEffects,
            note.WoreOffAt,
            note.Note,
            [.. note.Regimen.Select(m => new RegimenMedicineResponse(
                m.MedicineId.Value,
                m.Name,
                m.Dosage,
                MedicineEffectNotes.DosesOn(schedules.GetValueOrDefault(m.MedicineId), note.Date)))],
            note.LoggedBy.Value);
}

public sealed record RegimenMedicineResponse(Guid MedicineId, string Name, string Dosage, IReadOnlyList<RegimenDoseResponse> Doses);

public sealed record RegimenDoseResponse(TimeOnly Time, DoseStatus Status);
