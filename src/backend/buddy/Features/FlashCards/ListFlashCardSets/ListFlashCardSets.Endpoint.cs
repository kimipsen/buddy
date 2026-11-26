using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class ListFlashCardSetsEndpoint
{
    public static RouteGroupBuilder MapListFlashCardSets(this RouteGroupBuilder flashCards)
    {
        flashCards.MapGet("/sets", async Task<Results<Ok<IReadOnlyCollection<FlashCardSetResponse>>, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<FlashCardSet>>>(ListFlashCardSets.FromClaims(principal), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<FlashCardSet>>.Success(var sets) =>
                    TypedResults.Ok<IReadOnlyCollection<FlashCardSetResponse>>([.. sets.Select(FlashCardSetResponse.FromSet)]),
                Result<IReadOnlyCollection<FlashCardSet>>.Forbidden => TypedResults.Forbid(),
                // The handler never produces NotFound or Validation; neither is in this route's declared results.
                Result<IReadOnlyCollection<FlashCardSet>>.NotFound => TypedResults.Forbid(),
                Result<IReadOnlyCollection<FlashCardSet>>.Validation => TypedResults.Forbid(),
            };
        })
        .WithName("ListFlashCardSets");

        return flashCards;
    }
}
