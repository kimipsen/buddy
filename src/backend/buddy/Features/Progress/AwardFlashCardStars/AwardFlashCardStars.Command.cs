using buddy.Features.Users;

namespace buddy.Features.Progress;

// Internal command only -- no HTTP endpoint, like RecordStarChange. Called by the FlashCards finish
// handler through IMessageBus.InvokeAsync, without the try/catch SetTaskCompletionHandler wraps
// around RecordStarChange: here the stars are the point of the request, so a failure (a lost race,
// a concurrency conflict) must reach the caller. SetId is the FlashCardSetId's Guid, so Progress
// takes no dependency on the FlashCards feature. TierStars is the tier's full star value.
public sealed record AwardFlashCardStars(UserId ChildId, Guid SetId, DateOnly Day, int TierStars, Guid GameId);

public sealed record AwardFlashCardStarsResult(int Awarded);
