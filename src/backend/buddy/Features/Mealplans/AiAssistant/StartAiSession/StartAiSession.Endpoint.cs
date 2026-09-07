using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class StartAiSessionEndpoint
{
    public static RouteGroupBuilder MapStartAiSession(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ai/sessions", async Task<Results<Ok<AiSessionView>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            StartAiSessionRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = StartAiSession.FromClaims(
                principal,
                new UserId(childId),
                request.From,
                request.To,
                request.Slots,
                [.. request.MustIncludeMealIds.Select(id => new MealId(id))],
                request.Notes);

            var result = await bus.InvokeAsync<Result<AiSessionView>>(command, cancellationToken);

            return result switch
            {
                Result<AiSessionView>.Success(var view) => TypedResults.Ok(view),
                Result<AiSessionView>.Forbidden => TypedResults.Forbid(),
                Result<AiSessionView>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<AiSessionView>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("StartAiSession");

        return mealplans;
    }
}

public sealed record StartAiSessionRequest(DateOnly From, DateOnly To, IReadOnlyCollection<MealSlot> Slots, IReadOnlyCollection<Guid> MustIncludeMealIds, string? Notes);
