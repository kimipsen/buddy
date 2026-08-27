using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Pickups;

public static class AssignPickupEndpoint
{
    public static RouteGroupBuilder MapAssignPickup(this RouteGroupBuilder pickups)
    {
        pickups.MapPut("/children/{childId:guid}/assignments", async Task<Results<Ok<PickupOccurrence>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            PickupSlot slot,
            AssignPickupRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AssignPickup.FromClaims(
                principal,
                new UserId(childId),
                date,
                slot,
                request.Kind,
                request.GuardianId is { } guardianId ? new UserId(guardianId) : null,
                request.SiblingChildId is { } siblingChildId ? new UserId(siblingChildId) : null,
                request.PlaydateHostName,
                request.PlaydateLocation,
                request.PlaydateContactInfo,
                request.Time,
                request.Notes);

            var result = await bus.InvokeAsync<Result<PickupOccurrence>>(command, cancellationToken);

            return result switch
            {
                Result<PickupOccurrence>.Success(var occurrence) => TypedResults.Ok(occurrence),
                Result<PickupOccurrence>.Forbidden => TypedResults.Forbid(),
                Result<PickupOccurrence>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<PickupOccurrence>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("AssignPickup");

        return pickups;
    }
}

public sealed record AssignPickupRequest(
    PickupAssigneeKind Kind,
    Guid? GuardianId,
    Guid? SiblingChildId,
    string? PlaydateHostName,
    string? PlaydateLocation,
    string? PlaydateContactInfo,
    TimeOnly? Time,
    string? Notes);
