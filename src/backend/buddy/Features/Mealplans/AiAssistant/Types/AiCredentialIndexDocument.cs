namespace buddy.Features.Mealplans;

// Resolves "which AiProviderCredential stream was provisioned through child X". Written once on
// AiCredentialsInitialized, never updated or removed afterwards -- so it keeps naming that child
// even after the child is unlinked. MealFamilyResolution.ResolveFamilyAiCredentialIdAsync looks
// across the family (and the caller's unlinked children) and picks deterministically; see
// docs/backend/mealplans/flow.md, "AI credential resolution".
public sealed record AiCredentialIndexDocument(Guid Id, Guid ChildId);
