namespace buddy.Features.Pickups;

// Unauthenticated by design -- the token itself is the credential, mirroring Mealplans'
// GetMealPlanIcalFeed.Command.cs.
public sealed record GetPickupIcalFeed(PickupFeedId FeedId, string Token);
