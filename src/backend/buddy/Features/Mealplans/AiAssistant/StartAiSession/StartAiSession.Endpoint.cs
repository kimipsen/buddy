using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class StartAiSessionEndpoint
{
    public static RouteGroupBuilder MapStartAiSession(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ai/sessions", async Task<Results<Ok<AiSessionView>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>>> (
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
                FreeText.Normalize(request.Notes),
                request.RatedOnly,
                request.ServedWithin);

            var outcome = await bus.InvokeAsync<AiSessionOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.AiAssistantPolicy)
        .WithName("StartAiSession");

        return mealplans;
    }
}

public sealed record StartAiSessionRequest(DateOnly From, DateOnly To, IReadOnlyCollection<MealSlot> Slots, IReadOnlyCollection<Guid> MustIncludeMealIds, string? Notes = null,
    bool RatedOnly = false, AiServedWindow ServedWithin = AiServedWindow.Any);
