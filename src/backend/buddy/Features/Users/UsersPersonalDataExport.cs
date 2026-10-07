using buddy.Common.Erasure;

namespace buddy.Features.Users;

// The "account" section: the caller's own profile, as GET /users/me returns it (no Keycloak subject
// or email verification token hash).
public sealed class UsersPersonalDataExporter(IUserEventStore users) : IPersonalDataExporter
{
    public Type Store => typeof(IUsersStore);

    public string Section => "account";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken) =>
        await users.FindSnapshotAsync(subject.UserId, cancellationToken) is { } user ? UserResponse.FromUser(user) : null;
}
