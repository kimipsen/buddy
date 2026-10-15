using buddy.Features.Users;

namespace buddy.Features.Medicines;

// One day's effect and side-effect observations of a child's medicine
// (docs/backend/analysis/medicine-effect-notes.md, Question 3). Only what the guardian rated is
// stored: an area missing from Effects was not observed, which is not SideEffectSeverity.None.
// Note follows the FreeText convention -- "" means none.
public sealed record MedicineEffectNote(
    DateOnly Date,
    IReadOnlyList<EffectRating> Effects,
    IReadOnlyList<SideEffectRating> SideEffects,
    TimeOnly? WoreOffAt,
    string Note,
    IReadOnlyList<RegimenMedicine> Regimen,
    UserId LoggedBy)
{
    // Record equality compares the lists by reference, so content comparison (for the
    // append-only-if-changed rule) walks them. Regimen and LoggedBy are ignored: the regimen is kept
    // from the first log, and a re-save by the other guardian isn't a change (LogSleepEntry's rule).
    public bool HasSameContentAs(MedicineEffectNote other) =>
        WoreOffAt == other.WoreOffAt
        && Note == other.Note
        && Effects.SequenceEqual(other.Effects)
        && SideEffects.SequenceEqual(other.SideEffects);
}

public sealed record EffectRating(EffectArea Area, EffectLevel Level);

public sealed record SideEffectRating(SideEffect Kind, SideEffectSeverity Severity);

// The medicine as it was on the note's date: a copy, because MedicineSchedule keeps only its current
// Dosage (Question 4).
public sealed record RegimenMedicine(MedicineId MedicineId, string Name, string Dosage);

// Persisted by name (EnumStorage.AsString): add values, never rename them.
public enum EffectArea
{
    Focus,
    Calm,
    ImpulseControl,
    Mood
}

public enum EffectLevel
{
    Poor,
    Okay,
    Good
}

public enum SideEffect
{
    ReducedAppetite,
    TroubleFallingAsleep,
    Stomachache,
    Headache,
    Irritability,
    ReboundWhenWearingOff,
    Tics,
    FlatOrNotThemselves,
    Other
}

public enum SideEffectSeverity
{
    None,
    Mild,
    Moderate,
    Severe
}
