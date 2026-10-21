namespace buddy.Features.Mealplans;

// The Mealie household the token's user belongs to. Mealie scopes meal plans and shopping lists to
// it and recipes to its group; GroupSlug builds the "Open in Mealie" link (/g/{groupSlug}/r/{slug}).
public sealed record MealieHousehold(Guid HouseholdId, string HouseholdName, Guid GroupId, string GroupSlug);
