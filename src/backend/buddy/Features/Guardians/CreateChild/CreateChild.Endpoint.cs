using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class CreateChildEndpoint
{
    public static RouteGroupBuilder MapCreateChild(this RouteGroupBuilder children)
    {
        children.MapPost("/", async Task<Results<Ok<ChildResponse>, UnauthorizedHttpResult, Conflict<string>, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            CreateChildRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateChild.FromClaims(
                principal,
                request.GivenName.Trim(),
                request.FamilyName.Trim(),
                request.Username.Trim(),
                request.Kind);
            var result = await bus.InvokeAsync<CreateChildOutcome>(command, cancellationToken);

            return result switch
            {
                CreateChildOutcome.Success(var child, var link, var username, var temporaryPassword) =>
                    TypedResults.Ok(ChildResponse.FromChild(child, link, username, temporaryPassword)),
                CreateChildOutcome.Unauthenticated => TypedResults.Unauthorized(),
                CreateChildOutcome.UsernameUnavailable => TypedResults.Conflict("That username is already in use."),
                CreateChildOutcome.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            };
        })
        .WithName("CreateChild");

        return children;
    }
}

public sealed record CreateChildRequest(string GivenName, string FamilyName, string Username, GuardianKind Kind = GuardianKind.Guardian);

// Username and TemporaryPassword are shown exactly once, in this response -- neither is persisted
// or retrievable again (see the doc's "guardian is given the credential out of band"). The child
// has no email, so this username/password pair is the only way to log in as them.
public sealed record ChildResponse(UserId Id, Name Name, GuardianLinkId GuardianLinkId, GuardianKind Kind, string Username, string TemporaryPassword)
{
    public static ChildResponse FromChild(User child, GuardianLink link, string username, string temporaryPassword) =>
        new(child.Id, child.Name, link.Id, link.Kind, username, temporaryPassword);
}
