namespace buddy.Features.Feelings;

// Unauthenticated by design -- a doctor has no Buddy account. The token in the URL is the
// authentication, exactly like GetSharedSleepDiary. From/To are optional: the handler defaults to
// the last 14 days ending today on the child's own clock, which it only knows once the token has
// named the child.
public sealed record GetSharedFeelings(string Token, DateOnly? From, DateOnly? To);
