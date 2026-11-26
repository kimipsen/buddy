using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class DisableFlashCardSetEndpoint
{
    public static RouteGroupBuilder MapDisableFlashCardSet(this RouteGroupBuilder flashCards)
    {
        flashCards.MapDelete("/children/{childId:guid}/sets/{setId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid setId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = DisableFlashCardSet.FromClaims(principal, new UserId(childId), new FlashCardSetId(setId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // The handler never produces Validation; BadRequest is not in this route's declared results.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("DisableFlashCardSet");

        return flashCards;
    }
}
