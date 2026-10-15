using buddy.Features.Users;

namespace buddy.Features.Medicines;

// Deliberately equal to the child's own UserId -- a child has exactly one effect log, so finding it
// needs no index document (the SleepDiaryId trick). No collision inside the "medicines" schema:
// MedicineId and MedicineSharingId are both UUIDv7. See
// docs/backend/analysis/medicine-effect-notes.md, Question 2.
public sealed record MedicineEffectLogId(Guid Value)
{
    public static MedicineEffectLogId ForChild(UserId childId) => new(childId.Value);
}
