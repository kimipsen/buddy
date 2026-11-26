using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class GetFlashCardDeckEndpoint
{
    public static RouteGroupBuilder MapGetFlashCardDeck(this RouteGroupBuilder flashCards)
    {
        flashCards.MapGet("/me/sets/{setId:guid}/deck", async Task<Results<Ok<FlashCardDeckResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid setId,
            Guid tierId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = GetFlashCardDeck.FromClaims(principal, new FlashCardSetId(setId), new FlashCardTierId(tierId));
            var result = await bus.InvokeAsync<Result<IReadOnlyList<DeckCard>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyList<DeckCard>>.Success(var cards) => TypedResults.Ok(new FlashCardDeckResponse(cards)),
                Result<IReadOnlyList<DeckCard>>.NotFound => TypedResults.NotFound(),
                Result<IReadOnlyList<DeckCard>>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // The handler never produces Forbidden; it is not in this route's declared results.
                Result<IReadOnlyList<DeckCard>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("GetFlashCardDeck");

        return flashCards;
    }
}
