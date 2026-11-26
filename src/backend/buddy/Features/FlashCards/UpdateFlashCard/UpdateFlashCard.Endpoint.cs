using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class UpdateFlashCardEndpoint
{
    public static RouteGroupBuilder MapUpdateFlashCard(this RouteGroupBuilder flashCards)
    {
        flashCards.MapPut("/sets/{setId:guid}/cards/{cardId:guid}", async Task<Results<Ok<FlashCardSetResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid setId,
            Guid cardId,
            FlashCardRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateFlashCard.FromClaims(
                principal,
                new FlashCardSetId(setId),
                new FlashCardId(cardId),
                request.Question.Trim(),
                request.Answer.Trim());

            var result = await bus.InvokeAsync<Result<FlashCardSet>>(command, cancellationToken);

            return result switch
            {
                Result<FlashCardSet>.Success(var set) => TypedResults.Ok(FlashCardSetResponse.FromSet(set)),
                Result<FlashCardSet>.Forbidden => TypedResults.Forbid(),
                Result<FlashCardSet>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<FlashCardSet>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateFlashCard");

        return flashCards;
    }
}
