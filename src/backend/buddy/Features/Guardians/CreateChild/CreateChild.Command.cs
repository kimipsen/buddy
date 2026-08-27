using System.Security.Claims;

using buddy.Common.Validation;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record CreateChild(UserId? GuardianId, string GivenName, string FamilyName, string Username, GuardianKind Kind)
{
    public static CreateChild FromClaims(ClaimsPrincipal principal, string givenName, string familyName, string username, GuardianKind kind) =>
        new(principal.GetUserId(), givenName, familyName, username, kind);
}

// Distinct from the shared Result<T>: same reasoning as CreateGroupOutcome -- there's no existing
// resource here to hide behind an ambiguous 404, so an unauthenticated caller gets a 401.
public union CreateChildOutcome(CreateChildOutcome.Success, CreateChildOutcome.Unauthenticated, CreateChildOutcome.UsernameUnavailable, CreateChildOutcome.Validation)
{
    public sealed record Success(User Child, GuardianLink Link, string Username, string TemporaryPassword);
    public sealed record Unauthenticated;
    public sealed record UsernameUnavailable;
    public sealed record Validation(ValidationProblem Problem);
}
