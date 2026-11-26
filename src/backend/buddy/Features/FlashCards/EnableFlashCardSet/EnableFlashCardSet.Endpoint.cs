using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class EnableFlashCardSetEndpoint
{
    public static RouteGroupBuilder MapEnableFlashCardSet(this RouteGroupBuilder flashCards)
    {
        flashCards.MapPut("/children/{childId:guid}/sets/{setId:guid}", async Task<Results<Ok<EnabledFlashCardSetResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid setId,
            EnableFlashCardSetRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = EnableFlashCardSet.FromClaims(
                principal,
                new UserId(childId),
                new FlashCardSetId(setId),
                [.. (request.Tiers ?? []).Select(t => new FlashCardTierInput(t.Id is { } id ? new FlashCardTierId(id) : null, t.CardCount, t.Stars))]);

            var result = await bus.InvokeAsync<Result<EnabledFlashCardSet>>(command, cancellationToken);

            return result switch
            {
                Result<EnabledFlashCardSet>.Success(var enabled) => TypedResults.Ok(EnabledFlashCardSetResponse.FromEnabled(enabled)),
                Result<EnabledFlashCardSet>.Forbidden => TypedResults.Forbid(),
                Result<EnabledFlashCardSet>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<EnabledFlashCardSet>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("EnableFlashCardSet");

        return flashCards;
    }
}

// Id keeps an existing tier; leave it out for a new one. The tiers replace the set's current ones.
public sealed record FlashCardTierRequest(int CardCount, int Stars)
{
    public Guid? Id { get; init; }
}

public sealed record EnableFlashCardSetRequest(IReadOnlyList<FlashCardTierRequest> Tiers);
