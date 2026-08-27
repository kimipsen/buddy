using System.Security.Claims;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class SetGroupMemberRoleEndpoint
{
    public static RouteGroupBuilder MapSetGroupMemberRole(this RouteGroupBuilder groups)
    {
        groups.MapPut("/{groupId:guid}/members/{memberId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid memberId,
            SetGroupMemberRoleRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (request.Role == GroupRole.Owner)
            {
                return TypedResults.BadRequest(buddy.Common.Validation.ValidationProblem.Of("Ownership cannot be granted through this endpoint.").ToEnvelope(httpContext));
            }

            var command = SetGroupMemberRole.FromClaims(principal, new GroupId(groupId), new UserId(memberId), request.Role);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // SetGroupMemberRoleHandler never produces Validation, but BadRequest is already
                // part of this route's declared results (used above), so map it there if it ever did.
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("SetGroupMemberRole");

        return groups;
    }
}

public sealed record SetGroupMemberRoleRequest(GroupRole Role);
