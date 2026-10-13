using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class EditFeelingCheckInEndpoint
{
    public static RouteGroupBuilder MapEditFeelingCheckIn(this RouteGroupBuilder feelings)
    {
        feelings.MapPut("/children/{childId:guid}/check-ins/{checkInId:guid}", async Task<Results<Ok<FeelingCheckInResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid checkInId,
            EditFeelingCheckInRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = EditFeelingCheckIn.FromClaims(principal, new UserId(childId), new FeelingCheckInId(checkInId), request);
            var outcome = await bus.InvokeAsync<EditFeelingCheckInOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, EditFeelingCheckInOutcome.IsSelfReportCode)
        .WithName("EditFeelingCheckIn");

        return feelings;
    }
}

public sealed record EditFeelingCheckInRequest(
    FeelingZone Zone,
    DateOnly Date,
    TimeOnly Time,
    IReadOnlyList<FeelingTag>? Tags = null,
    string? Note = null);
