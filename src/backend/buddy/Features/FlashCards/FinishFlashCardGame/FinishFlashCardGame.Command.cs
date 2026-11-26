using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

// One first-pass slot. Question and Answer are only the echo fallback for a card that was removed
// mid-game; the server looks the text up by CardKey and ignores the echo while the card exists.
// RepeatedAnswer is accepted so the client can send what the game state holds, and changes nothing.
public sealed record FinishFlashCardGameResult(int Slot, string CardKey, bool KnewFirstTime, bool? RepeatedAnswer, string? Question, string? Answer);

public sealed record FinishFlashCardGame(
    UserId ChildId,
    Guid GameId,
    FlashCardSetId SetId,
    FlashCardTierId TierId,
    IReadOnlyList<FinishFlashCardGameResult> Results)
{
    public static FinishFlashCardGame FromClaims(
        ClaimsPrincipal principal,
        Guid gameId,
        FlashCardSetId setId,
        FlashCardTierId tierId,
        IReadOnlyList<FinishFlashCardGameResult> results) =>
        new(principal.GetRequiredUserId(), gameId, setId, tierId, results);
}

public sealed record FinishedFlashCardGame(int Stars, int TotalStars, FlashCardGameSummary Game);

// Feature-specific outcome: two business-rule rejections that render as 422 with their own codes.
public union FinishFlashCardGameOutcome(
    FinishFlashCardGameOutcome.Success,
    FinishFlashCardGameOutcome.NotFound,
    FinishFlashCardGameOutcome.Validation,
    FinishFlashCardGameOutcome.GameIdReused,
    FinishFlashCardGameOutcome.GameNoLongerAvailable)
{
    public const string GameIdReusedCode = "game_id_reused";
    public const string GameNoLongerAvailableCode = "game_no_longer_available";

    public sealed record Success(FinishedFlashCardGame Finished);
    public sealed record NotFound;
    public sealed record Validation(buddy.Common.Validation.ValidationProblem Problem);
    // A GameId identifies exactly one game (one set and tier).
    public sealed record GameIdReused;
    // The guardian disabled the set or removed the tier before any stars were awarded for this game.
    public sealed record GameNoLongerAvailable;
}
