using Marten.Events.Aggregation;

namespace buddy.Features.Users;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct UserId(Guid Value)" -- as a document's Id. UserId
// here is a sealed record (a class), which Marten's DocumentMapping rejects with "Could not
// determine an 'id/Id' field or property". So the snapshot document can't be User itself; this
// thin wrapper carries the plain Guid Marten needs alongside the actual User value. UserId stays
// untouched everywhere else in the codebase -- this wrapper exists purely at the snapshot-storage
// boundary. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record UserSnapshot(Guid Id, User User);

// Inline snapshot of User, maintained by Marten in the same transaction as every event append
// (see UsersFeature.AddUsersFeature: options.Projections.Register(new UserSnapshotProjection(),
// ...)). Stored in the shared "snapshots" schema, never the "users" event schema -- it is derived,
// rebuildable state, not a second source of truth. GuardianLinked/GuardianKindChanged/
// GuardianRevoked/GuardianInviteCreated/GuardianInviteAccepted/GuardianInviteRevoked have no case
// here, the same way they have no case in User.Fold: those events belong to GuardianLink's and
// GuardianLinkInvite's own streams, which merely happen to live in this same Marten store/schema.
public sealed class UserSnapshotProjection : SingleStreamProjection<UserSnapshot, Guid>
{
    public static UserSnapshot Create(UserCreated created) =>
        new(created.UserId.Value, User.Fold(null, UserEvent.FromPayload(created))!);

    public UserSnapshot Apply(UserSnapshot current, NameUpdated updated) =>
        current with { User = User.Fold(current.User, UserEvent.FromPayload(updated))! };

    public UserSnapshot Apply(UserSnapshot current, TimeZoneUpdated updated) =>
        current with { User = User.Fold(current.User, UserEvent.FromPayload(updated))! };

    public UserSnapshot Apply(UserSnapshot current, LanguageUpdated updated) =>
        current with { User = User.Fold(current.User, UserEvent.FromPayload(updated))! };

    public UserSnapshot Apply(UserSnapshot current, EmailUpdated updated) =>
        current with { User = User.Fold(current.User, UserEvent.FromPayload(updated))! };

    public UserSnapshot Apply(UserSnapshot current, EmailVerificationRequested requested) =>
        current with { User = User.Fold(current.User, UserEvent.FromPayload(requested))! };

    public UserSnapshot Apply(UserSnapshot current, EmailVerified verified) =>
        current with { User = User.Fold(current.User, UserEvent.FromPayload(verified))! };

    public UserSnapshot Apply(UserSnapshot current, UserDeleted deleted) =>
        current with { User = User.Fold(current.User, UserEvent.FromPayload(deleted))! };
}
