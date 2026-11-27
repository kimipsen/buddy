using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class RemoveFlashCardEndpoint
{
    public static RouteGroupBuilder MapRemoveFlashCard(this RouteGroupBuilder flashCards)
    {
        flashCards.MapDelete("/sets/{setId:guid}/cards/{cardId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid setId,
            Guid cardId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = RemoveFlashCard.FromClaims(principal, new FlashCardSetId(setId), new FlashCardId(cardId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // Only the read-only rule of a built-in set.
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("RemoveFlashCard");

        return flashCards;
    }
}
