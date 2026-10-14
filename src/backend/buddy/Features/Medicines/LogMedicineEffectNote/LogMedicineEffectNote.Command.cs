using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record LogMedicineEffectNote(
    UserId UserId,
    UserId ChildId,
    DateOnly Date,
    IReadOnlyList<EffectRating> Effects,
    IReadOnlyList<SideEffectRating> SideEffects,
    TimeOnly? WoreOffAt,
    string Note)
{
    // Ratings are stored in catalog order, so the same ratings sent in another order are the same
    // note (no event) and every reader sees one order.
    public static LogMedicineEffectNote FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly date, LogMedicineEffectNoteRequest request) =>
        new(
            principal.GetRequiredUserId(),
            childId,
            date,
            [.. (request.Effects ?? []).OrderBy(r => r.Area)],
            [.. (request.SideEffects ?? []).OrderBy(r => r.Kind)],
            request.WoreOffAt,
            FreeText.Normalize(request.Note));

    public MedicineEffectNote ToNote(IReadOnlyList<RegimenMedicine> regimen) =>
        new(Date, Effects, SideEffects, WoreOffAt, Note, regimen, UserId);
}
