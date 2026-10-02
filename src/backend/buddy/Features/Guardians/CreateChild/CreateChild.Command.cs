using System.Security.Claims;

using buddy.Common.Validation;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record CreateChild(UserId GuardianId, string GivenName, string FamilyName, string Username, GuardianKind Kind)
{
    public static CreateChild FromClaims(ClaimsPrincipal principal, string givenName, string familyName, string username, GuardianKind kind) =>
        new(principal.GetRequiredUserId(), givenName, familyName, username, kind);
}

// Distinct from the shared Result<T>: a taken username is its own 409 outcome, and there's no
// existing resource here to hide behind an ambiguous 404.
public union CreateChildOutcome(CreateChildOutcome.Success, CreateChildOutcome.UsernameUnavailable, CreateChildOutcome.Validation)
{
    public sealed record Success(User Child, GuardianLink Link, string Username, string TemporaryPassword);
    public sealed record UsernameUnavailable;
    public sealed record Validation(ValidationProblem Problem);
}
