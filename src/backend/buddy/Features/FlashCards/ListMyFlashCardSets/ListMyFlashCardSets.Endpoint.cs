using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class ListMyFlashCardSetsEndpoint
{
    public static RouteGroupBuilder MapListMyFlashCardSets(this RouteGroupBuilder flashCards)
    {
        flashCards.MapGet("/me/sets", async Task<Results<Ok<IReadOnlyList<MyFlashCardSetResponse>>, NotFound>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<PlayableFlashCardSet>>>(ListMyFlashCardSets.FromClaims(principal), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<PlayableFlashCardSet>>.Success(var sets) =>
                    TypedResults.Ok<IReadOnlyList<MyFlashCardSetResponse>>([.. sets.Select(MyFlashCardSetResponse.From)]),
                Result<IReadOnlyCollection<PlayableFlashCardSet>>.NotFound => TypedResults.NotFound(),
                // The handler never produces these; they are not in this route's declared results.
                Result<IReadOnlyCollection<PlayableFlashCardSet>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<PlayableFlashCardSet>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMyFlashCardSets");

        return flashCards;
    }
}
