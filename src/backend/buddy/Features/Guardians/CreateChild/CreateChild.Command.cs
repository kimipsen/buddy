using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record CreateChild(UserId? GuardianId, string Name, GuardianKind Kind)
{
    public static CreateChild FromClaims(ClaimsPrincipal principal, string name, GuardianKind kind) =>
        new(principal.GetUserId(), name, kind);
}

// Distinct from the shared Result<T>: same reasoning as CreateGroupOutcome -- there's no existing
// resource here to hide behind an ambiguous 404, so an unauthenticated caller gets a 401.
public union CreateChildOutcome(CreateChildOutcome.Success, CreateChildOutcome.Unauthenticated)
{
    public sealed record Success(User Child, GuardianLink Link, string Username, string TemporaryPassword);
    public sealed record Unauthenticated;
}
