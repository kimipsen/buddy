using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.TransitionWarnings;

public static class ConfigureTransitionWarningsEndpoint
{
    public static RouteGroupBuilder MapConfigureTransitionWarnings(this RouteGroupBuilder transitionWarnings)
    {
        transitionWarnings.MapPut("/children/{childId:guid}", async Task<Results<Ok<ChildTransitionWarningsResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            ConfigureTransitionWarningsRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var quietHours = request.QuietHours is { } quiet ? new QuietHours(quiet.From, quiet.To) : null;
            var command = ConfigureTransitionWarnings.FromClaims(
                principal, new UserId(childId), request.Enabled, request.LeadMinutes, request.Kinds, quietHours);
            var result = await bus.InvokeAsync<Result<ChildTransitionWarningsResponse>>(command, cancellationToken);

            return result switch
            {
                Result<ChildTransitionWarningsResponse>.Success(var response) => TypedResults.Ok(response),
                Result<ChildTransitionWarningsResponse>.Forbidden => TypedResults.Forbid(),
                Result<ChildTransitionWarningsResponse>.NotFound => TypedResults.NotFound(),
                Result<ChildTransitionWarningsResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("ConfigureTransitionWarnings");

        return transitionWarnings;
    }
}

// The whole settings form, saved as one unit (full replace). QuietHours null means none.
public sealed record ConfigureTransitionWarningsRequest(
    bool Enabled,
    IReadOnlyList<int> LeadMinutes,
    IReadOnlyList<WarningKind> Kinds,
    QuietHoursRequest? QuietHours = null);

public sealed record QuietHoursRequest(TimeOnly From, TimeOnly To);
