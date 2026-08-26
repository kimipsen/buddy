using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class ListAssignableMembersEndpoint
{
    public static RouteGroupBuilder MapListAssignableMembers(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}/assignable-members", async Task<Results<Ok<IReadOnlyCollection<AssignableMemberResponse>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListAssignableMembers.FromClaims(principal, new CalendarId(calendarId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<AssignableMemberSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<AssignableMemberSummary>>.Success(var members) =>
                    TypedResults.Ok<IReadOnlyCollection<AssignableMemberResponse>>([.. members.Select(AssignableMemberResponse.FromSummary)]),
                Result<IReadOnlyCollection<AssignableMemberSummary>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<AssignableMemberSummary>>.NotFound => TypedResults.NotFound(),
                // CheckContribute never returns Validation, so this is unreachable today -- there's
                // no BadRequest in this route's declared results, so it collapses to NotFound like
                // other routes in this codebase do.
                Result<IReadOnlyCollection<AssignableMemberSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListAssignableCalendarMembers");

        return calendars;
    }
}

public sealed record AssignableMemberResponse(Guid UserId, string GivenName, string FamilyName)
{
    public static AssignableMemberResponse FromSummary(AssignableMemberSummary summary) =>
        new(summary.Id.Value, summary.Name.GivenName, summary.Name.FamilyName);
}
