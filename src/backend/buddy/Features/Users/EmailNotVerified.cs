using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace buddy.Features.Users;

// The caller's own email is the address an action was meant for, but it isn't verified yet --
// accepting a group invite or a guardian invite. An unverified address could have been typed in by
// someone other than its real owner, so it can't be trusted to claim something sent to it.
//
// 403 with the `email_not_verified` ErrorEnvelope code, so a client can tell "verify your email
// first" apart from the plain 403 for an invite sent to a different address. Handlers return
// EmailNotVerified as a case of their feature-specific outcome union (it doesn't fit Result<T>'s
// four cases); endpoints render it with ToForbidden.
public sealed record EmailNotVerified(string Message);

public static class EmailNotVerifiedExtensions
{
    public const string ErrorCode = "email_not_verified";

    public static JsonHttpResult<ErrorEnvelope> ToForbidden(this EmailNotVerified notVerified, HttpContext context) =>
        TypedResults.Json(
            new ErrorEnvelope(ErrorCode, notVerified.Message, new Dictionary<string, string[]>(), context.TraceIdentifier),
            statusCode: StatusCodes.Status403Forbidden);
}
