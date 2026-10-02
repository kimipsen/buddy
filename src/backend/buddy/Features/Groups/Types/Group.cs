using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Calendars;
using buddy.Features.Mealplans;
using buddy.Features.Medicines;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record Group(
    GroupId Id,
    string Name,
    ImmutableDictionary<UserId, GroupRole> Members,
    ImmutableDictionary<GroupRole, CalendarRole> CalendarPermissionPolicy,
    ImmutableDictionary<GroupRole, MealplanAccessTier> MealplanPermissionPolicy,
    ImmutableDictionary<GroupRole, MedicineAccessTier> MedicinePermissionPolicy,
    bool IsDeleted = false)
{
    public static Group? Rehydrate(IEnumerable<GroupEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static Group Replay(IEnumerable<GroupEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so GroupSnapshotProjection can
    // drive the same logic one Marten-delivered event at a time instead of duplicating this switch.
    // Deliberately not named Apply/Create -- those names are a convention JasperFx's projection
    // source generator scans for on any type used as a projection document, and Group is that
    // document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static Group Start(GroupEvent @event) => @event switch
    {
        GroupCreated created => new Group(
            created.GroupId,
            created.Name,
            ImmutableDictionary<UserId, GroupRole>.Empty.Add(created.OwnerId, GroupRole.Owner),
            created.CalendarPermissionPolicy,
            created.MealplanPermissionPolicy,
            created.MedicinePermissionPolicy),
        _ => throw EventReplay.NotAStartEvent(nameof(Group), @event.EventType)
    };

    public static Group Advance(Group group, GroupEvent @event) => @event switch
    {
        GroupMemberRoleGranted granted => group with { Members = group.Members.SetItem(granted.MemberId, granted.Role) },
        GroupMemberRoleRevoked revoked => group with { Members = group.Members.Remove(revoked.MemberId) },
        GroupCalendarPolicyUpdated updated => group with { CalendarPermissionPolicy = updated.Policy },
        GroupMealplanPolicyUpdated updated => group with { MealplanPermissionPolicy = updated.Policy },
        GroupMedicinePolicyUpdated updated => group with { MedicinePermissionPolicy = updated.Policy },
        GroupDeleted => group with { IsDeleted = true },
        // Invites live in the same stream but are read through GroupInviteDocument, not Group.
        GroupInviteCreated or GroupInviteAccepted or GroupInviteRevoked => group,
        GroupCreated => throw EventReplay.AlreadyStarted(nameof(Group), @event.EventType)
    };
}
