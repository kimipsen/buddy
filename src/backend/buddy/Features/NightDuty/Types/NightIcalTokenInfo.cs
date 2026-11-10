using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// A token covers one child's nights: ChildId is fixed when the token is issued.
public sealed record NightIcalTokenInfo(UserId ChildId, string Hash, DateTimeOffset IssuedAt);
