using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class UpdateFlashCardSetEndpoint
{
    public static RouteGroupBuilder MapUpdateFlashCardSet(this RouteGroupBuilder flashCards)
    {
        flashCards.MapPut("/sets/{setId:guid}", async Task<Results<Ok<FlashCardSetResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid setId,
            UpdateFlashCardSetRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateFlashCardSet.FromClaims(principal, new FlashCardSetId(setId), request.Name.Trim(), request.Math);
            var result = await bus.InvokeAsync<Result<FlashCardSet>>(command, cancellationToken);

            return result switch
            {
                Result<FlashCardSet>.Success(var set) => TypedResults.Ok(FlashCardSetResponse.FromSet(set)),
                Result<FlashCardSet>.Forbidden => TypedResults.Forbid(),
                Result<FlashCardSet>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<FlashCardSet>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateFlashCardSet");

        return flashCards;
    }
}

// Math is only for a math set; leave it out to keep the current rule.
public sealed record UpdateFlashCardSetRequest(string Name, MathRule? Math = null);
