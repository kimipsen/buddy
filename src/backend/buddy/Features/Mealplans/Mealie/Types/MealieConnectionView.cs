using System.Text.Json.Serialization;

namespace buddy.Features.Mealplans;

// The masked, read-facing shape of a family's MealieConnection -- the token is never returned,
// only its last four characters (the AiProviderSettings rule). Wire shape { "kind": 0 } |
// { "kind": 1, "baseUrl", ... }; [JsonPolymorphic] for the reason given on
// TestProviderConnectionResult.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NotConnected), 0)]
[JsonDerivedType(typeof(Connected), 1)]
public abstract record MealieConnectionView
{
    public sealed record NotConnected : MealieConnectionView;

    public sealed record Connected(
        string BaseUrl,
        string HouseholdName,
        string MealieUserName,
        string MealieVersion,
        string TokenLast4,
        Guid ConnectedBy,
        DateTimeOffset ConnectedAt) : MealieConnectionView;

    public static MealieConnectionView From(MealieConnection? connection) => connection?.Current is { } current
        ? new Connected(
            current.BaseUrl,
            current.Household.HouseholdName,
            current.MealieUserName,
            current.MealieVersion,
            current.Token.Last4,
            current.ConnectedBy.Value,
            current.ConnectedAt)
        : new NotConnected();
}

// TestMealieConnection's answer. Failed is an expected outcome (the server is off, the token was
// revoked), not an error of the request itself.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Succeeded), 0)]
[JsonDerivedType(typeof(Failed), 1)]
public abstract record TestMealieConnectionResult
{
    public sealed record Succeeded(string MealieVersion, int RecipeCount, int ShoppingListCount) : TestMealieConnectionResult;

    public sealed record Failed(string Code, string Message) : TestMealieConnectionResult;
}
