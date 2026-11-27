using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class DeleteFlashCardSetEndpoint
{
    public static RouteGroupBuilder MapDeleteFlashCardSet(this RouteGroupBuilder flashCards)
    {
        flashCards.MapDelete("/sets/{setId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid setId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<Unit>>(DeleteFlashCardSet.FromClaims(principal, new FlashCardSetId(setId)), cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // Only the read-only rule of a built-in set.
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("DeleteFlashCardSet");

        return flashCards;
    }
}
