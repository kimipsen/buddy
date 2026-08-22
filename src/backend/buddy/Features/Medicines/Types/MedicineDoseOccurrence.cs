namespace buddy.Features.Medicines;

// A computed instance of a MedicineSchedule's daily dose times, resolved for one (Date, Time) slot
// and overlaid with that slot's recorded DoseStatus. Never persisted -- always recomputed from
// current MedicineSchedule state (see MedicineDoseExpansion), the same contract
// CalendarItemOccurrence already has for Calendars.
public sealed record MedicineDoseOccurrence(
    MedicineId MedicineId,
    string Name,
    string Dosage,
    string Icon,
    string Color,
    DateOnly Date,
    TimeOnly Time,
    DoseStatus Status);
