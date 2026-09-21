using Marten.Events.Aggregation;

namespace buddy.Features.Groups;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct GroupId(Guid Value)" -- as a document's Id. GroupId
// here is a sealed record (a class), which Marten's DocumentMapping rejects with "Could not
// determine an 'id/Id' field or property". So the snapshot document can't be Group itself; this
// thin wrapper carries the plain Guid Marten needs alongside the actual Group value. GroupId stays
// untouched everywhere else in the codebase -- this wrapper exists purely at the snapshot-storage
// boundary. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record GroupSnapshot(Guid Id, Group Group);

// Inline snapshot of Group, maintained by Marten in the same transaction as every event append
// (see GroupsFeature.AddGroupsFeature: options.Projections.Register(new GroupSnapshotProjection(),
// ...)). Stored in the shared "snapshots" schema, never the "groups" event schema -- it is
// derived, rebuildable state, not a second source of truth. GroupInviteCreated/Accepted/Revoked
// have no case here, the same way they have no case in Group.Fold: they don't change Group's own
// fields, only GroupInviteDocument.
public sealed class GroupSnapshotProjection : SingleStreamProjection<GroupSnapshot, Guid>
{
    public GroupSnapshot Create(GroupCreated created) =>
        new(created.GroupId.Value, Group.Fold(null, GroupEvent.FromPayload(created))!);

    public GroupSnapshot Apply(GroupSnapshot current, GroupMemberRoleGranted granted) =>
        current with { Group = Group.Fold(current.Group, GroupEvent.FromPayload(granted))! };

    public GroupSnapshot Apply(GroupSnapshot current, GroupMemberRoleRevoked revoked) =>
        current with { Group = Group.Fold(current.Group, GroupEvent.FromPayload(revoked))! };

    public GroupSnapshot Apply(GroupSnapshot current, GroupCalendarPolicyUpdated updated) =>
        current with { Group = Group.Fold(current.Group, GroupEvent.FromPayload(updated))! };

    public GroupSnapshot Apply(GroupSnapshot current, GroupMealplanPolicyUpdated updated) =>
        current with { Group = Group.Fold(current.Group, GroupEvent.FromPayload(updated))! };

    public GroupSnapshot Apply(GroupSnapshot current, GroupMedicinePolicyUpdated updated) =>
        current with { Group = Group.Fold(current.Group, GroupEvent.FromPayload(updated))! };

    public GroupSnapshot Apply(GroupSnapshot current, GroupDeleted deleted) =>
        current with { Group = Group.Fold(current.Group, GroupEvent.FromPayload(deleted))! };
}
