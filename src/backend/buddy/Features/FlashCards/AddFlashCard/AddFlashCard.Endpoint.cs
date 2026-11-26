using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class AddFlashCardEndpoint
{
    public static RouteGroupBuilder MapAddFlashCard(this RouteGroupBuilder flashCards)
    {
        flashCards.MapPost("/sets/{setId:guid}/cards", async Task<Results<Ok<FlashCardSetResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid setId,
            FlashCardRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AddFlashCard.FromClaims(principal, new FlashCardSetId(setId), request.Question.Trim(), request.Answer.Trim());
            var result = await bus.InvokeAsync<Result<FlashCardSet>>(command, cancellationToken);

            return result switch
            {
                Result<FlashCardSet>.Success(var set) => TypedResults.Ok(FlashCardSetResponse.FromSet(set)),
                Result<FlashCardSet>.Forbidden => TypedResults.Forbid(),
                Result<FlashCardSet>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<FlashCardSet>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("AddFlashCard");

        return flashCards;
    }
}

// Shared by AddFlashCard and UpdateFlashCard.
public sealed record FlashCardRequest(string Question, string Answer);
