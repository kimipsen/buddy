namespace buddy.Features.Mealplans;

// Resolves "what is child X's AiProviderCredential stream ID" -- one per family, addressed the
// same way MealPlanIndexDocument resolves a family's MealPlan stream. Written once on
// AiCredentialsInitialized, never updated or removed afterwards.
public sealed record AiCredentialIndexDocument(Guid Id, Guid ChildId);
