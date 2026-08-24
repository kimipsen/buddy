namespace buddy.Features.Medicines;

// Resolves "what is child X's MedicineSharing stream ID" -- mirrors MealPlanIndexDocument (a
// MedicineSharing is a 1:1 singleton per child, but Marten streams are still addressed by their
// own aggregate ID). Written once on the first MedicineSharedWithGroup for a child, never updated
// or removed afterwards.
public sealed record MedicineSharingIndexDocument(Guid Id, Guid ChildId);
