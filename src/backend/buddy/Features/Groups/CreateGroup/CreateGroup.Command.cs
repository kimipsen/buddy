using System.Security.Claims;

using buddy.Common.Validation;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record CreateGroup(UserId UserId, string Name)
{
    public static CreateGroup FromClaims(ClaimsPrincipal principal, string name) =>
        new(principal.GetRequiredUserId(), name);
}

// Creating a group can't fail for a provisioned caller beyond its validation, so there's no
// NotFound/Forbidden case the shared Result<T> would force the endpoint to map.
public union CreateGroupOutcome(CreateGroupOutcome.Success, CreateGroupOutcome.Validation)
{
    public sealed record Success(GroupWithMemberDetails Group);
    public sealed record Validation(ValidationProblem Problem);
}
