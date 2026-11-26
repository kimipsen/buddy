using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class CreateFlashCardSetEndpoint
{
    public static RouteGroupBuilder MapCreateFlashCardSet(this RouteGroupBuilder flashCards)
    {
        flashCards.MapPost("/sets", async Task<Results<Ok<FlashCardSetResponse>, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            CreateFlashCardSetRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateFlashCardSet.FromClaims(principal, request.Name.Trim(), request.Kind, request.Math);
            var result = await bus.InvokeAsync<Result<FlashCardSet>>(command, cancellationToken);

            return result switch
            {
                Result<FlashCardSet>.Success(var set) => TypedResults.Ok(FlashCardSetResponse.FromSet(set)),
                Result<FlashCardSet>.Forbidden => TypedResults.Forbid(),
                Result<FlashCardSet>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // Creating never looks anything up, so there is nothing to not find.
                Result<FlashCardSet>.NotFound => TypedResults.Forbid(),
            };
        })
        .WithName("CreateFlashCardSet");

        return flashCards;
    }
}

public sealed record CreateFlashCardSetRequest(string Name, FlashCardSetKind Kind, MathRule? Math = null);
