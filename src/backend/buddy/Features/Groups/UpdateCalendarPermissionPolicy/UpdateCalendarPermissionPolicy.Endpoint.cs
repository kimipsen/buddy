using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class UpdateCalendarPermissionPolicyEndpoint
{
    public static RouteGroupBuilder MapUpdateCalendarPermissionPolicy(this RouteGroupBuilder groups)
    {
        groups.MapPut("/{groupId:guid}/calendar-permission-policy", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            UpdateCalendarPermissionPolicyRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            foreach (var role in Enum.GetValues<GroupRole>())
            {
                if (!request.Policy.ContainsKey(role))
                {
                    return TypedResults.BadRequest(buddy.Common.Validation.ValidationProblem.Of($"The policy must include an entry for every group role; '{role}' is missing.").ToEnvelope(httpContext));
                }
            }

            var policy = request.Policy.ToImmutableDictionary();
            var command = UpdateCalendarPermissionPolicy.FromClaims(principal, new GroupId(groupId), policy);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // UpdateCalendarPermissionPolicyHandler never produces Validation, but BadRequest
                // is already part of this route's declared results (used above), so map it there
                // if it ever did.
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("UpdateGroupCalendarPermissionPolicy");

        return groups;
    }
}

public sealed record UpdateCalendarPermissionPolicyRequest(IReadOnlyDictionary<GroupRole, CalendarRole> Policy);
