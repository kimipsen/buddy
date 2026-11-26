using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class GetChildFlashCardsEndpoint
{
    public static RouteGroupBuilder MapGetChildFlashCards(this RouteGroupBuilder flashCards)
    {
        flashCards.MapGet("/children/{childId:guid}", async Task<Results<Ok<ChildFlashCardsResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<FlashCardPractice>>(GetChildFlashCards.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<FlashCardPractice>.Success(var practice) => TypedResults.Ok(ChildFlashCardsResponse.FromPractice(practice)),
                Result<FlashCardPractice>.Forbidden => TypedResults.Forbid(),
                Result<FlashCardPractice>.NotFound => TypedResults.NotFound(),
                // The handler never produces Validation; BadRequest is not in this route's declared results.
                Result<FlashCardPractice>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetChildFlashCards");

        return flashCards;
    }
}
