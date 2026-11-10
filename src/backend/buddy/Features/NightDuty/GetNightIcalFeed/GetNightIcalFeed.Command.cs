namespace buddy.Features.NightDuty;

// Unauthenticated by design -- the token itself is the credential, mirroring Pickups'
// GetPickupIcalFeed.
public sealed record GetNightIcalFeed(NightFeedId FeedId, string Token);
