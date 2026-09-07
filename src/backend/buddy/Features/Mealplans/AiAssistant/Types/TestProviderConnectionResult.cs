namespace buddy.Features.Mealplans;

// IsSuccessful=false is a normal, expected outcome here (a bad/expired key, no quota, ...) --
// distinct from the command itself failing validation/authorization, which still goes through the
// usual Result<T> NotFound/Forbidden/Validation cases.
public sealed record TestProviderConnectionResult(bool IsSuccessful, string? ErrorMessage);
