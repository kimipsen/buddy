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
                FreeText.Normalize(request.Notes));

            var outcome = await bus.InvokeAsync<StartAiSessionOutcome>(command, cancellationToken);

            return outcome switch
            {
                StartAiSessionOutcome.Success(var view) => TypedResults.Ok(view),
                StartAiSessionOutcome.Forbidden => TypedResults.Forbid(),
                StartAiSessionOutcome.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                StartAiSessionOutcome.NotFound => TypedResults.NotFound(),
                StartAiSessionOutcome.DataSharingNotAcknowledged => TypedResults.Conflict(new ErrorEnvelope(
                    StartAiSessionOutcome.DataSharingNotAcknowledgedCode,
                    "A guardian has to acknowledge what the assistant shares with the AI provider before the family's first session.",
                    new Dictionary<string, string[]>(),
                    httpContext.TraceIdentifier)),
            };
        })
        .RequireRateLimiting(RateLimitingFeature.AiAssistantPolicy)
        .WithName("StartAiSession");

        return mealplans;
    }
}

public sealed record StartAiSessionRequest(DateOnly From, DateOnly To, IReadOnlyCollection<MealSlot> Slots, IReadOnlyCollection<Guid> MustIncludeMealIds, string? Notes = null);
