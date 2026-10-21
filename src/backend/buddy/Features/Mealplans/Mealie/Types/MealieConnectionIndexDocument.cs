namespace buddy.Features.Mealplans;

// Resolves "which MealieConnection stream was provisioned through child X" -- the same shape as
// AiCredentialIndexDocument: written once, moved only by erasure, and resolved across the family by
// MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync.
public sealed record MealieConnectionIndexDocument(Guid Id, Guid ChildId);
