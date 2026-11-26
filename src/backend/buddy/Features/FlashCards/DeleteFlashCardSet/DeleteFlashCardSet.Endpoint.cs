using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class DeleteFlashCardSetEndpoint
{
    public static RouteGroupBuilder MapDeleteFlashCardSet(this RouteGroupBuilder flashCards)
    {
        flashCards.MapDelete("/sets/{setId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid setId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<Unit>>(DeleteFlashCardSet.FromClaims(principal, new FlashCardSetId(setId)), cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // The handler never produces Validation; BadRequest is not in this route's declared results.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("DeleteFlashCardSet");

        return flashCards;
    }
}
